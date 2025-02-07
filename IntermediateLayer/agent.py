import time
import random

class Agent:
    def __init__(self, id,bStation, init_pos=('','')):
        self.id=id
        self.init_pos=init_pos
        self.decided=False
        self.currProd=None
        self.goingToProd=0
        self.haveProd=0
        self.goingToCombProd=False
        self.haveCombProd=False

        self.lastAction=0
        self.currJob=(0,0)
        self.firstOrder=("","")
        self.secondOrder=("","")
        self.firstStation=None
        self.secondStation=None
        self.currLocation=("","")
        self.finishedOrder=True
        self.actionSended=True
        self.combTo=''

        self.assignedToInit=False
        self.inInit=True

        #self.startedGrabbing=False
        self.grabingComb=False
        self.grabingProd=None
        self.grabbedProd=None
        self.startingTimeGrabbing=-1
        self.timeGrabbing=-1
        self.startedGrabbing=False
        self.lbTimeGrabbing=2
        self.ubTimeGrabbing=2

        self.dropingComb=False
        self.dropingProd=None
        self.droppedProd=None
        self.startingTimeDropping=-1
        self.timeDropping=-1
        self.startedDropping=False
        self.lbTimeDroping=2
        self.ubTimeDroping=2

        self.bStation=bStation
        

    def AssignAction(self,nextJob,action,prod,outStation,inStation):

        self.inInit=False
        self.currJob=nextJob
        self.goingToProd=action
        print(f"agent {self.id} is assigned to product {action}")
        self.lastAction=action
        self.currProd=prod
        # if outStation.name == self.firstOrder[0] and inStation.name == self.secondOrder[0]:
        #     k=0
        self.firstOrder=(outStation.name,"OUTPUT")
        self.secondOrder=(inStation.name,"INPUT")
        self.firstStation=outStation
        self.secondStation=inStation
        self.decided=True

    def AssignCombAction(self,station):
        self.decided=True
        self.combTo=station.name
        self.secondStation=station

    def getCurrPosition(self):
        return (3,3)
    
    # def GiveProduct(self):
    #     self.lastAction=self.haveProd
    #     #self.products[self.agents[msg.robot_id].lastAction].
    #     self.haveProd=0

    def getCurrOrder(self):
        if self.goingToProd >0 :
            return self.firstOrder
        if self.haveProd>0 : #self.haveCombProd or
            return self.secondOrder
        return ("","")



    def Update(self):
        if self.grabingProd is not None:
            self.GrabProduct(self.grabingProd)
        elif self.grabingComb:
            self.GrabCombProduct()
        if self.dropingProd is not None:
            self.DropProduct()
        elif self.dropingComb:
            self.DropCombProduct()


    def GrabProduct(self,prod):
        station = prod.currStation
        if station ==None:
            k=0
        if not prod.blocked and (station.name=="C-BS" or station.readyProd.id==prod.id ) :
            if not self.startedGrabbing :
                print(f"agent {self.id} started grabbing product {prod.id}")
                self.startedGrabbing=True
                self.grabingProd=prod
                self.startingTimeGrabbing=time.time()
                self.timeGrabbing=random.uniform(self.lbTimeDroping,self.ubTimeDroping)
            elif time.time()>= self.startingTimeGrabbing+self.timeGrabbing:
                print(f"agent {self.id} grabed product {prod.id}")
                if station.name=="C-BS":
                    station.GiveProduct(prod)
                else:
                    station.GiveProduct()
                
                # if not prod.JobInSameStation():
                #     station.UnAssign()

                prod.SetParentAgent(self)
                self.grabbedProd=self.grabingProd
                self.grabingProd=None
                self.firstStation=None
                self.firstOrder=("","")
                prod.grabbed=True
                self.haveProd=prod.id
                self.goingToProd=-1
                self.startedGrabbing=False

    def getCurrAction(self):
        if not self.decided:
            return -1
        elif self.secondOrder==("",""):
            return 12
        else:
            return self.lastAction
        

    def GrabCombProduct(self):

        if not self.startedGrabbing :
            print(f"agent {self.id} started grabbing combproduct ")
            self.grabingComb=True
            self.startedGrabbing=True
            self.startingTimeGrabbing=time.time()
            self.timeGrabbing=random.uniform(self.lbTimeGrabbing,self.ubTimeDroping)

        elif time.time() >= self.startingTimeGrabbing + self.timeGrabbing:
            print(f"agent {self.id} started grabbed combproduct")
            self.bStation.GiveCombProduct()
            self.haveCombProd=True
            #self.secondStation=None
            #self.secondOrder=("","")
            self.startedGrabbing=False
            #self.startedGrabbing=False
            self.grabingComb=False

    def DropCombProduct(self):
        
        if not self.startedDropping:
            print(f"agent {self.id} started dropping combProduct ")
            self.startedDropping=True
            self.dropingComb=True
            self.startingTimeDropping=time.time()
            self.timeDropping=random.uniform(self.lbTimeDroping,self.ubTimeDroping)

        elif time.time()>= self.startingTimeDropping+self.timeDropping:
            print(f"agent {self.id} droped combProduct")
            self.decided=False
            self.combTo=''
            self.dropingComb=False
            self.haveCombProd=False
            self.startedDropping=False
            self.secondStation.ReceiveCombProd()



        # print(f"agent {self.id} started dropped a combProduct ")
        # self.decided=False
        # self.haveCombProd=False

    def DropProduct(self):
        if not self.startedDropping:
            print(f"agent {self.id} started dropping product {self.grabbedProd.id}")
            self.startedDropping=True
            self.dropingProd=self.grabbedProd
            self.startingTimeDropping=time.time()
            self.timeDropping=random.uniform(self.lbTimeDroping,self.ubTimeDroping)

        elif time.time()>= self.startingTimeDropping+self.timeDropping and self.secondStation.IsAvailable():
            print(f"agent {self.id} droped product {self.dropingProd.id}")
            self.decided=False
            self.haveProd=-1
            self.grabbedProd=None
            
            self.secondOrder=("","")
            self.dropingProd.grabbed=False
            self.dropingProd.assigned=False
            self.secondStation.ReceiveProduct(self.dropingProd)
            self.startedDropping=False
            self.dropingProd=None 
            

    # def GrabProduct(self):
    #     self.haveProd=self.goingToProd
    #     self.goingToProd=0
    #     self.currProd.agent=self
    #     self.currProd.station=None

    def RecevingCombProd(self):
        self.goingToCombProd = False
        self.haveCombProd = True

    def GoingToProd(self,id):
        self.goingToProd=id

    # def DropProduct(self,id):
    #     self.products[id]=0

    def WaitForAction(self):
        return self.decided
    
