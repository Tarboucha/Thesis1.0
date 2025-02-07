import time
import random

class Station:
    def __init__(self,  tasks=[], name=""):
        #self.episodeStart=episodeStart
        self.tasks = tasks
        self.name = name
        self.agentComing = 0 #willhave in code
        #self.prodComing=0
        self.willHave = 0
        self.available = True
        self.combRequired = 0
        self.combReceived = 0
        self.willReceive=0
        self.waitingProd = None
        self.processingProd = None
        self.readyProd = None
        self.blocked=False
        self.waiting=True
        self.outFree=True
        self.inFree=True
        self.nextTask=-1
        self.location=(0.0,0.0)
        # self.currTaskStartingTime = -1.0
        # self.currTaskProcessingTime = -1.0

    def getReadyProd(self):
        return self.readyProd

    def nProds(self):
        n=0
        if self.waitingProd!=None:
            n+=1
        if self.processingProd!=None:
            n+=1
        if self.readyProd!=None:
            n+=1
        return n
    
    def WillReceive(self,num):
        self.willReceive+=num

    def AssignInTask(self,requiredComb):
        if requiredComb>0:
            print(f"for the next task machine {self.name} requires {requiredComb} comb Products")
        self.assigned=True
        self.inFree = False
        self.combRequired=requiredComb
    
    def AssignOutTask(self):
        self.outFree=False

    def getPosition(self):
        return self.location ### à réécrire

    def WaitingAvailable(self):
        if self.waitingProd!=None:
            return False
        return True

    def IsAvailable(self):
        if self.waitingProd!=None or (self.processingProd!=None and self.readyProd!=None):
            return False
        return True

    def GetWaitingProd(self):
        return self.waitingProd

    def AssignTask(self,task,requiredComb):
        self.waiting=False
        self.willHave=True
        self.nextTask=task
        self.combRequired=requiredComb

    def IsWaitingForComb(self):
        if self.combRequired>self.combReceived+self.willReceive:
            return True
        return False
    
    def WillReceive(self,n):
        self.willReceive+=n

    def WillNotReceive(self,n):
        self.willReceive-=n

    def ReceiveCombProd(self):
        #self.inFree=True
        print(f"Station {self.name} received a combProduct")
        self.willReceive-=1
        self.combReceived+=1
        if(self.waitingProd!=None and self.combRequired!=0 and self.combReceived>=self.combRequired):
            self.combReceived=0
            self.combRequired=0
            if(self.processingProd==None):
                self.processingProd=self.waitingProd
                self.waitingProd=None
                self.processingProd.PerformTask(time.time())
        

    def HaveProd(self,id):
        if self.waitingProd!=None and self.waitingProd.id==id:
            return True
        if self.processingProd!=None and self.processingProd.id==id:
            return True
        if self.readyProd!=None and self.readyProd.id==id:
            return True
        return False

    def ReceiveProduct(self,prod):
        print(f"Station {self.name} received Product {prod.id} ")
        self.willHave=False
        self.blocked=True
        #self.inFree=TrueSetParentStation
        prod.SetParentStation(self)
        prod.blocked=True
        #print(f"machine {self.name} received Product {prod.id}")
        if(self.processingProd!=None):
            self.waitingProd=prod
        else:
            if self.combRequired==0 or self.combReceived>= self.combRequired:
                self.processingProd=prod
                self.combReceived=0
                self.combRequired=0
                prod.PerformTask(time.time())
            else:
                self.waitingProd=prod

        # self.products = id
        # if self.combRequired==0 or self.combRequired==self.combReceived:
        #     self.processing=True
        #     prod.currTaskStartingTime=time.time()
        #     prod.currTaskProcessingTime=prod.jobs[prod.currJobPointer][1]

    def GiveProduct(self):
        #self.outFree=True
#        self.blocked=False
        print(f"Machine {self.name} gave product {self.readyProd.id}")
        if(self.processingProd!=None and not self.processingProd.processing):
            self.readyProd=self.processingProd
            self.processingProd=None
            self.readyProd.blocked=False
            self.blocked=False
        else:
            self.blocked=False
            self.readyProd=None

    def CurrProductFinish(self):
        if(self.readyProd==None):
            self.blocked=False
            self.readyProd=self.processingProd
            self.processingProd=None
            self.readyProd.blocked=False
        if(self.waitingProd!=None):
            if self.combRequired==0 or self.combReceived>= self.combRequired:
                self.processingProd=self.waitingProd
                self.waitingProd=None
                self.combReceived=0
                self.combRequired=0
                self.processingProd.PerformTask(time.time())

    def GetNextProd(self):
        if self.readyProd!=None:
            return self.readyProd
        elif self.processingProd!=None:
            return self.processingProd
        elif self.waitingProd!=None:
            return self.waitingProd
        return None

    def IsAvailable(self):
        if(self.waitingProd!=None or self.processingProd!=None and self.readyProd!=None):
            return False
        return True

    def GetReadyProd(self):
        return self.readyProd

class RingStation(Station):
    pass


class DeliveryStation(Station):
    def __init__(self,n_products,tasks=[], name=""):
        super().__init__(tasks, name)
        self.finishedProducts={}
        self.n_products=n_products
        for i in range(n_products):
            self.finishedProducts[i+1]=None

    def CurrProductFinish(self):
        self.finishedProducts[self.processingProd.id]=self.processingProd
        self.processingProd=None
        if(self.waitingProd!=None):
            self.processingProd=self.waitingProd
            self.waitingProd=None
            self.processingProd.PerformTask(time.time())

    def IsAvailable(self):
        if self.readyProd==None:
            return True
        return False

    
    



class BaseStation(Station):

    def __init__(self,episodeStart, tasks=[], name=""):
        super().__init__(tasks, name)
        self.episodeStart=episodeStart
        self.lastTimeTaken=time.time()
        self.readyToGive=False
        self.preparationTime=5.0
        self.HaveProds={}
        self.preparationStart=episodeStart
        self.preparationFinished=-1.0


    def Update(self):
        if len(self.HaveProds)>0:
            for _,prod in self.HaveProds.items():
                if prod.currJobPointer==0 and prod.startingTime<time.time()-self.episodeStart:
                    prod.blocked=False
                    prod.currJobPointer=1
                        
        if not self.readyToGive and self.preparationStart+self.preparationTime<time.time():
            self.readyToGive=True
            self.preparationFinished=time.time()-self.episodeStart
            self.preparationTime=random.uniform(5.0,7.0)


    def ReadyTime(self):
        if(self.readyToGive):
            return self.preparationFinished
        else:
            return self.preparationStart-self.episodeStart+self.preparationTime

    def ReceiveProduct(self,prod):
        self.HaveProds[prod.id]=prod
        prod.currStation=self
        prod.blocked=True

    def GiveCombProduct(self):
        self.assigned=False
        #self.outFree=True
        self.readyToGive=False
        self.preparationStart=time.time()

    def GiveProduct(self,prod):
        del self.HaveProds[prod.id]
        #self.outFree=True
        self.readyToGive=False
        self.preparationStart=time.time()

    # def GiveProduct(self):
    #     self.blocked=True
    #     self.lastTimeTaken=time.time()
    #     self.readyToGive=False
    #     if(self.processing!=None and not self.processing.processing):
    #         self.readyProd=self.processing
    #         self.readyProd.Unblock()
    #     else:
    #         self.readyProd=None
    #     pass

    def ReadyToGive(self):
        return self.readyToGive





    # def Update(self):
    #     if self.product>0 and self.processing==False and self.combRequired==self.combReceived:
    #         self.processing=True
    #         self.currTaskStartingTime=time.time()
    #         self.currTaskProcessingTime=self.prod.jobs[self.prod.currJobPointer][1]
        
    #     if self.processing and self.currTaskProcessingTime<= time.time() - self.currTaskStartingTime:
    #         self.processing=False
    #         self.prod.CurrJobFinished()

    # def Available(self):
    #     if self.available and self.willHave<=0:
    #         return True
    #     return False            

    # def GiveProduct(self,id):
    #     self.availabe=True
    #     self.combReceived=0
    #     self.combRequired=0
    #     self.products[id]=0

    # def ProdInStation(self):
    #     return self.product

