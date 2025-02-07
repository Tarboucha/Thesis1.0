class CombProduct:
    def __init__(self, episodeStart, jobs=[(0,0)], startingTime=0.0,n_rings=0, id=0):
        self.episodeStart=episodeStart
        self.startingTime=startingTime
        self.processStart=0
        self.finished=False
        self.id=id
        self.jobs=jobs
        self.jobsFinishingTime=[-1.0]*(3+n_rings)
        self.currJobPointer=0
        self.n_rings=n_rings
        self.currStation=None
        self.currAgent=None
        self.currAgentId=0
        self.grabbed=False
        self.processing=False
        self.assigned=False
        self.available=False
        self.blocked=False