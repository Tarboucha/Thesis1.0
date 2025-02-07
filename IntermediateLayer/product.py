import time

#job : first entry task, second entry processingTime
class Product:
    def __init__(self, episodeStart,startingStation,taskToStationsName, jobs=[(0,0)], startingTime=0.0,n_rings=0, id=0):
        self.episodeStart=episodeStart
        self.startingTime=startingTime
        self.processStart=0
        self.finished=False
        self.id=id
        self.jobs=jobs
        self.jobsFinishingTime=[-1.0]*(3+n_rings)
        self.currJobPointer=0
        self.n_rings=n_rings
        self.currStation=startingStation
        self.currAgent=None
        self.currAgentId=0
        self.grabbed=False
        self.processing=False
        self.assigned=False
        self.taskToStationsName=taskToStationsName
        #self.available=False
        self.blocked=False

    def AddFinishingTime(self,t,ind):
        self.jobsFinishingTime[ind]=t

    def getCurrPosition(self):
        return (0.0,0.0)

    def getJob(self,ind):
        return self.jobs[ind]

    def getNTaskFinished(self):
        for i in range(len(self.jobsFinishingTime)):
            if self.jobsFinishingTime[i]==-1.0:
                return i
        return len(self.jobsFinishingTime)

    def getTaskFinishingTime(self, ind):
        return self.jobsFinishingTime[ind]

    def ContinueTask(self,t):
        if t> self.processStart+self.jobs[self.currJobPointer][1]:
            print(f"prod {self.id} finished performing task {self.currJobPointer}")
            processTime=time.time() - self.episodeStart
            self.processStart=0
            self.processing=False
            self.jobsFinishingTime[self.currJobPointer]=processTime
            #case of last station needed?
            if self.currJobPointer<len(self.jobs)-1:
                self.currJobPointer+=1
            else:
                self.finished=True
                print(f"product {self.id} is finished")
                self.currStation.assign=False
                self.currStation.blocked=False
            self.grabbed=False
            self.assigned=False
            if self.currStation.name!="C-BS":
                self.currStation.CurrProductFinish()

    def LastStation(self):

        if len(self.jobs)-1==self.currJobPointer:
            return True
        return False
    

    def PerformTask(self,init_time):
        print(f"prod {self.id} started performing task {self.currJobPointer}")
        self.processStart=init_time
        if init_time< self.processStart +self.jobs[self.currJobPointer][1]:
            self.processing=True
        
        else:
            print(f"prod {self.id} finished performing task {self.currJobPointer}")
            self.jobsFinishingTime[self.currJobPointer]=init_time-self.episodeStart
            #case of last station needed?
            self.processStart=0
            self.processing=False
            if self.currJobPointer<len(self.jobs)-1:
                self.currJobPointer+=1
            else:
                self.finished=True
                self.currStation.assign=False
                self.currStation.blocked=False

            self.processing=False
            self.available=True
            self.grabbed=False
            self.assigned=False
            self.currStation.CurrProductFinish()


    def RemainingTime(self):
        if self.blocked:
            return self.jobs[self.currJobPointer][1]-(time.time()-self.processStart)
        return 0
   
    # doesn't work since jobs contains the taks number not the station's id
    def JobInSameMachine(self):
        if self.currJobPointer>0 and self.taskToStationsName[self.jobs[self.currJobPointer][0]] == self.taskToStationsName[self.jobs[self.currJobPointer-1][0]]:
            return True
        return False

    def SetParentStation(self,station):
        self.currStation=station
        self.currAgent=None

    def SetParentAgent(self,agent):
        self.currAgent=agent
        self.currStation=None

    def getNextJob(self):
        return self.jobs[self.currJobPointer+1]

    def getCurrJob(self):
        return self.jobs[self.currJobPointer]

    def getPrevJob(self):
        return self.jobs[self.currJobPointer-1]

    def givenToStation(self):
        self.currTaskStartingTime=time.time()
        self.currTaskProcessingTime=self.jobs[self.currJobPointer][1]

    def Update(self):

        # if self.currJobPointer==0 and not self.available and self.currStation.readyToGive and time.time()-self.episodeStart > self.startingTime:
        #     self.processing=False
        #     self.available=True
        #     self.currJobPointer+=1
        if self.currStation is not None and self.currStation.name!='C-BS' and self.currStation.name!='C-DS' and not self.currStation.HaveProd(self.id):
            k=0

        if not self.finished and self.processing and self.currJobPointer>0 and self.blocked:
            self.ContinueTask(time.time())

            
        # if self.processing and self.currTaskProcessingTime< time.time() - self.currTaskStartingTime:
        #     self.processing=False
        #     self.available=True
        #     self.currJobPointer+=1

    
    def finishTask(self,startingTime):
        self.jobFinishingTime[self.currJobPointer] = startingTime-time.time()
        self.currJobPointer+=1
