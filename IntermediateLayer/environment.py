
import time
import numpy as np
import json
import socket
import random
from msg import Msg
import agent
import sys
import math
#import logging

from Model import ModelOnnx
from protoRF import AgentTaskHandler
from protoRF import RobotInfoHandler
from station import Station,RingStation,BaseStation,DeliveryStation
from product import Product
from contextlib import redirect_stdout


# class RStation(Station):
#     def __init__(self, tasks=[], name="", n_products=11):
#         super().__init__(tasks,name,n_products)
#         self.combRequired=0
#         self.combReceived=0

class UDPReceiver(threading.Thread):
    def __init__(self, ip, port, buffer_size=5120):
        super().__init__()
        self.ip = ip
        self.port = port
        self.buffer_size = buffer_size
        self.sock = None
        self.running = False
        self.message_queue = queue.Queue()

    def setup_socket(self):
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.setblocking(False)
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, self.buffer_size)
        
        self.sock.bind(('', self.port))
        print(f"UDP socket bound to {self.ip}:{self.port} for receiving")

    def run(self):
        self.setup_socket()
        self.running = True
        while self.running:
            try:
                data, addr = self.sock.recvfrom(1024)
                self.message_queue.put((data, addr))
            except socket.error as e:
                if e.errno != errno.EAGAIN and e.errno != errno.EWOULDBLOCK:
                    print(f"Socket error: {e}")

    def stop(self):
        self.running = False
        if self.sock:
            self.sock.close()


class Environment:
    def __init__(self,config_path):
        with open(config_path,'r') as file:
            self.config = json.load(file)


        self.nn=False
        self.spt=False
        self.lpt=True

        #d=self.distances["C-CS1"]["C-DS"]
        self.send_file_path = self.config["send_file_path"]  # Path to the file containing messages to be broadcasted
        self.receive_file_path = self.config['receive_file_path']
        self.log=""
        self.n_agents = self.config["n_agents"]
        self.n_bstations = 1
        self.n_rstations = 2
        self.n_cstations = 2
        self.n_dstations = 1
        self.n_stations = self.n_bstations + self.n_rstations + self.n_cstations + self.n_dstations 
        self.n_products = 11
        self.a_speed = 0.45
        self.first_failure=0
        self.tasksToStationsName = {1:'C-BS', 2:'C-RS1', 3:'C-RS1', 4:'C-RS2', 5:'C-RS2', 6:'C-CS1', 7:'C-CS2', 8:'C-DS'}
        self.ip = self.config["ip"]
        self.port=self.config["port"]
        self.episodeStart=time.time()
        self.agent_ips={'172.26.108.81':1,'172.26.108.82':2,'172.26.108.83':3}
        self.agents_pos={}
        self.maxEpisodeTime=1200.0
        self.minZ = -4
        self.maxZ = 4
        self.minX = -7
        self.maxX = 7
        self.maxD =   math.sqrt((self.maxZ - self.minZ) ** 2 + (self.maxX - self.minX) ** 2)




        self.m_distances = [[0.0]*self.n_stations for i in range(self.n_stations)]
        self.stations = {}
        self.products = {}

        self.stations["C-BS"] = BaseStation(tasks=[1],name="C-BS",episodeStart=self.episodeStart)
        self.stations["C-DS"] = DeliveryStation(n_products=self.n_products,tasks=[8],name="C-DS")
        self.stations["C-RS1"] = RingStation(tasks=[2,3],name="C-RS1")
        self.stations["C-RS2"] = RingStation(tasks=[4,5],name="C-RS2")
        self.stations["C-CS1"] = Station(tasks=[6],name="C-CS1")
        self.stations["C-CS2"] = Station(tasks=[7],name="C-CS2")
        self.AssignStationsLocations(self.config["stations_path"])
        self.distances={}

        for name,station in self.stations.items():
            self.distances[name]={}
            for name2,station2 in self.stations.items():
                self.distances[name][name2]=self.computeDist(station.getPosition(),station2.getPosition())


        # with open(self.config["distances_path"], 'r') as f:
        #     self.distances = json.load(f)
        self.robot_info=RobotInfoHandler()
        self.agents = {}
        self.agents_proto = {}
        
        for i in range(self.n_agents):
            #init position should be specified in the config.json file
            init_pos=("M-RS1","INPUT")
            if i==1:
                init_pos=("M-RS2","INPUT")
            robot_id=self.config["agent"+str(i+1)+"_id"]
            self.agents[robot_id]=agent.Agent(id=robot_id,init_pos=(self.config["agent"+str(i+1)+"_init_waypoint"],self.config["agent"+str(i+1)+"_init_machine_point"]),bStation=self.stations["C-BS"])
            self.agents_proto[robot_id]=AgentTaskHandler(robot_id=robot_id,
                                                         init_waypoint=self.config["agent"+str(i+1)+"_init_waypoint"],
                                                         init_machine_point=self.config["agent"+str(i+1)+"_init_machine_point"])
            self.agents_pos[robot_id]=(0.0,0.0)


        #test
        self.agents_proto[3].redo=True


        self.taskPayments={}
        self.taskPayments[self.config["1paymentTask"]]=1
        self.taskPayments[self.config["2paymentTask"]]=2

        #self.paymentsToStation={}
        #self.paymentsToStation[1]=self.config["1paymentTask"]
        #self.paymentsToStation[2]=self.config["1paymentTask"]

        for i in range(0,10):
            if i not in self.taskPayments:
                self.taskPayments[i]=0

        #deadlock management
        self.deadlock=False
        self.deadlockProds=[-1]*2

        self.model=ModelOnnx(model_path=self.config["model_path"])

        self.InitProducts()

        self.startingTime=time.time()
        

        #observationVariables
        self.h = 0
        self.last_h = 0
        self.reward = 0
        self.cumulative_reward = 0
        self.verbose = True
        self.debbug_product_ID = 0
        self.lower_bounds = [0] * 10  # Adjust size as needed

        self.speed = 0.45
        self.lbGrabbingTime=1
        self.lbDroppingTime=1
        self.ubGrabbingTime=1
        self.ubDroppingTime=1

        self.message=None

    def SPT(self,agent_id):

        current_time=-1
        chosen=-1
        chosen2=-1
        otherAgentsActions=[-1]*len(self.agents)
        if not self.agents[agent_id].decided:
        
            ind=0
            for id,agent in self.agents.items():
                if id!=agent_id and agent.decided:
                    otherAgentsActions[ind]=agent.getCurrAction()
                    ind+=1
            
            if self.stations["C-RS1"].IsWaitingForComb() and self.stations["C-RS1"].inFree and self.stations["C-BS"].outFree: #and self.stations["C-RS1"].inFree 
                chosen2=1
                # self.stations["C-RS1"].WillReceive(1)
                # self.agents[agent_id].AssignCombAction(self.stations["C-RS1"])

            elif self.stations["C-RS2"].IsWaitingForComb() and self.stations["C-RS2"].inFree and self.stations["C-BS"].outFree:
                chosen2=1
                # self.stations["C-RS2"].WillReceive(1)
                # self.agents[agent_id].AssignCombAction(self.stations["C-RS2"])
        
        if chosen2<0 and (self.deadlock or self.CheckDeadlock()):
            for prod_id in range(1, self.n_products + 1):
                prod = self.products[prod_id]

                if not prod.finished and prod.id not in otherAgentsActions and not prod.grabbed and not prod.assigned: #and not prod.blocked 
                    stationName=self.tasksToStationsName[prod.getCurrJob()[0]]
                    station = self.stations[stationName]

                    if stationName[:4]=="C-RS":
                        indD=-1
                        for i in range(len(self.deadlockProds)):
                            if self.deadlockProds[i]==prod.id:
                                indD=i
                                break
                        
                        if indD>-1 and not prod.blocked:
                            ind = prod.getPrevJob()[0]
                            prevStationName=self.tasksToStationsName[ind]
                            prevStation = self.stations[prevStationName]
                            temp_task_time= time.time() + self.distances[prevStationName][stationName]/self.speed 
                            temp_task_time += prod.getCurrJob()[1]

                            if (prevStation.GetNextProd() is not None and prevStation.GetNextProd().id == prod.id and (current_time<0 or current_time >temp_task_time) 
                                and prevStation.outFree and station.inFree and station.GetWaitingProd() is None):
                                
                                agent.stepAction = prod.id
                                current_time=temp_task_time
                                chosen = prod.id
                        elif not prod.blocked:
                            
                            ind = prod.getPrevJob()[0]
                            prevStationName=self.tasksToStationsName[ind]
                            prevStation = self.stations[prevStationName]
                            temp_task_time= time.time() + self.distances[prevStationName][stationName]/self.speed 
                            temp_task_time += prod.getCurrJob()[1]

                            if (prevStation.GetNextProd() is not None and prevStation.GetNextProd().id == prod.id and (current_time<0 or current_time >temp_task_time) 
                                and prevStation.outFree and station.inFree and (prod.JobInSameMachine() or 
                                (prod.LastStation() and not station.GetWaitingProd() or station.IsAvailable()))):
                                agent.stepAction = prod.id
                                current_time=temp_task_time
                                chosen = prod.id

                        elif (not prod.LastStation() and station.GetNextProd() is not None and station.GetNextProd().id==prod.id ):
                            expected_time=time.time()+prod.RemainingTime()
                            nJob = prod.getNextJob()
                            nextStationName = self.tasksToStationsName[nJob[0]]
                            nextStation = self.stations[nextStationName]
                            expected_time += self.distances[stationName][nextStationName]/self.speed
                            expected_time+= nJob[1]
                            if nextStation.IsAvailable() and (current_time < 0 or current_time > expected_time):
                                agent.stepAction = prod.id
                                current_time=expected_time
                                chosen = 0 
                    else:    
                        if (prod.currJobPointer ==0 or(prod.currJobPointer==1 and prod.getNTaskFinished()==0)):
                            temp_task_time = self.computeDist(self.agents_pos[agent_id],self.stations[stationName].getPosition())
                            expected_time = self.stations["C-BS"].ReadyTime()
                            expected_time = max(expected_time, prod.startingTime)
                            expected_time = max (time.time()+temp_task_time - self.startingTime, expected_time)
                            if (current_time<0 or current_time>expected_time) and self.stations["C-BS"].outFree and station.inFree and station.IsAvailable():
                                    agent.stepAction = prod.id
                                    current_time=temp_task_time
                                    chosen = 0
                                    if(self.stations["C-BS"].ReadyToGive() and not prod.blocked):
                                        chosen=prod.id
                        
                        else:
                            if not prod.blocked:
                                ind = prod.getPrevJob()[0]
                                prevStationName=self.tasksToStationsName[ind]
                                prevStation = self.stations[prevStationName]
                                temp_task_time= time.time() + self.distances[prevStationName][stationName]/self.speed 
                                temp_task_time += prod.getCurrJob()[1]

                                if (prevStation.GetNextProd() is not None and prevStation.GetNextProd().id == prod.id and (current_time<0 or current_time >temp_task_time) 
                                    and prevStation.outFree and station.inFree and (prod.JobInSameMachine() or 
                                    (prod.LastStation() and not station.GetWaitingProd() or station.IsAvailable()))):
                                    agent.stepAction = prod.id
                                    current_time=temp_task_time
                                    chosen = prod.id

                                elif (not prod.LastStation() and station.GetNextProd() is not None and station.GetNextProd().id==prod.id ):
                                    expected_time=time.time()+prod.RemainingTime()
                                    nJob = prod.getNextJob()
                                    nextStationName = self.tasksToStationsName[nJob[0]]
                                    nextStation = self.stations[nextStationName]
                                    expected_time += self.distances[stationName][nextStationName]/self.speed
                                    expected_time+= nJob[1]
                                    if nextStation.IsAvailable() and (current_time < 0 or current_time > expected_time):
                                        agent.stepAction = prod.id
                                        current_time=expected_time
                                        chosen = 0 

        elif chosen2<0 and chosen<0:

            for prod_id in range(1, self.n_products + 1):
                prod = self.products[prod_id]

                if not prod.finished and prod.id not in otherAgentsActions and not prod.grabbed and not prod.assigned:
                    stationName=self.tasksToStationsName[prod.getCurrJob()[0]]
                    station = self.stations[stationName]

                    if (prod.currJobPointer ==0 or(prod.currJobPointer==1 and prod.getNTaskFinished()==0)):
                        temp_task_time = self.computeDist(self.agents_pos[agent_id],self.stations[stationName].getPosition())
                        expected_time = self.stations["C-BS"].ReadyTime()
                        expected_time = max(expected_time, prod.startingTime)
                        expected_time = max (time.time()+temp_task_time - self.startingTime, expected_time)
                        if (current_time<0 or current_time>expected_time) and self.stations["C-BS"].outFree and station.inFree and station.IsAvailable():
                                agent.stepAction = prod.id
                                current_time=temp_task_time
                                chosen = 0
                                if(self.stations["C-BS"].ReadyToGive() and not prod.blocked):
                                    chosen=prod.id
                    
                    else:
                        if not prod.blocked:
                            ind = prod.getPrevJob()[0]
                            prevStationName=self.tasksToStationsName[ind]
                            prevStation = self.stations[prevStationName]
                            temp_task_time= time.time() + self.distances[prevStationName][stationName]/self.speed 
                            temp_task_time += prod.getCurrJob()[1]

                            if (prevStation.GetNextProd() is not None and prevStation.GetNextProd().id == prod.id and (current_time<0 or current_time >temp_task_time) 
                                and prevStation.outFree and station.inFree and (prod.JobInSameMachine() or 
                                (prod.LastStation() and not station.GetWaitingProd()) or station.IsAvailable())):
                                agent.stepAction = prod.id
                                current_time=temp_task_time
                                chosen = prod.id

                            elif (not prod.LastStation() and station.GetNextProd() is not None and station.GetNextProd().id==prod.id ):
                                expected_time=time.time()+prod.RemainingTime()
                                nJob = prod.getNextJob()
                                nextStationName = self.tasksToStationsName[nJob[0]]
                                nextStation = self.stations[nextStationName]
                                expected_time += self.distances[stationName][nextStationName]/self.speed
                                expected_time+= nJob[1]
                                if nextStation.IsAvailable() and (current_time < 0 or current_time > expected_time):
                                    agent.stepAction = prod.id
                                    current_time=temp_task_time
                                    chosen = 0 


        if chosen2>0: return self.n_products+1
        else: return chosen



    def LPT(self,agent_id):

        current_time=-1
        chosen=-1
        chosen2=-1
        otherAgentsActions=[-1]*len(self.agents)
        if not self.agents[agent_id].decided:
        
            ind=0
            for id,agent in self.agents.items():
                if id!=agent_id and agent.decided:
                    otherAgentsActions[ind]=agent.getCurrAction()
                    ind+=1
            
            if self.stations["C-RS1"].IsWaitingForComb() and self.stations["C-RS1"].inFree and self.stations["C-BS"].outFree: #and self.stations["C-RS1"].inFree 
                chosen2=1
                # self.stations["C-RS1"].WillReceive(1)
                # self.agents[agent_id].AssignCombAction(self.stations["C-RS1"])

            elif self.stations["C-RS2"].IsWaitingForComb() and self.stations["C-RS2"].inFree and self.stations["C-BS"].outFree:
                chosen2=1
                # self.stations["C-RS2"].WillReceive(1)
                # self.agents[agent_id].AssignCombAction(self.stations["C-RS2"])
        
        if chosen2<0 and (self.deadlock or self.CheckDeadlock()):
            for prod_id in range(1, self.n_products + 1):
                prod = self.products[prod_id]

                if not prod.finished and prod.id not in otherAgentsActions and not prod.grabbed and not prod.assigned: #and not prod.blocked 
                    stationName=self.tasksToStationsName[prod.getCurrJob()[0]]
                    station = self.stations[stationName]

                    if stationName[:4]=="C-RS":
                        indD=-1
                        for i in range(len(self.deadlockProds)):
                            if self.deadlockProds[i]==prod.id:
                                indD=i
                                break
                        
                        if indD>-1 and not prod.blocked:
                            ind = prod.getPrevJob()[0]
                            prevStationName=self.tasksToStationsName[ind]
                            prevStation = self.stations[prevStationName]
                            temp_task_time= time.time() + self.distances[prevStationName][stationName]/self.speed 
                            temp_task_time += prod.getCurrJob()[1]

                            if (prevStation.GetNextProd() is not None and prevStation.GetNextProd().id == prod.id and (current_time<0 or current_time < temp_task_time) 
                                and prevStation.outFree and station.inFree and station.GetWaitingProd() is None):
                                
                                agent.stepAction = prod.id
                                current_time=temp_task_time
                                chosen = prod.id
                        elif not prod.blocked:
                            
                            ind = prod.getPrevJob()[0]
                            prevStationName=self.tasksToStationsName[ind]
                            prevStation = self.stations[prevStationName]
                            temp_task_time= time.time() + self.distances[prevStationName][stationName]/self.speed 
                            temp_task_time += prod.getCurrJob()[1]

                            if (prevStation.GetNextProd() is not None and prevStation.GetNextProd().id == prod.id and (current_time<0 or current_time < temp_task_time) 
                                and prevStation.outFree and station.inFree and (prod.JobInSameMachine() or 
                                (prod.LastStation() and not station.GetWaitingProd() or station.IsAvailable()))):
                                agent.stepAction = prod.id
                                current_time=temp_task_time
                                chosen = prod.id

                        elif (not prod.LastStation() and station.GetNextProd() is not None and station.GetNextProd().id==prod.id ):
                            expected_time=time.time()+prod.RemainingTime()
                            nJob = prod.getNextJob()
                            nextStationName = self.tasksToStationsName[nJob[0]]
                            nextStation = self.stations[nextStationName]
                            expected_time += self.distances[stationName][nextStationName]/self.speed
                            expected_time+= nJob[1]
                            if nextStation.IsAvailable() and (current_time < 0 or current_time < expected_time):
                                agent.stepAction = prod.id
                                current_time=temp_task_time
                                chosen = 0 
                    else:    
                        if (prod.currJobPointer ==0 or(prod.currJobPointer==1 and prod.getNTaskFinished()==0)):
                            temp_task_time = self.computeDist(self.agents_pos[agent_id],self.stations[stationName].getPosition())
                            expected_time = self.stations["C-BS"].ReadyTime()
                            expected_time = max(expected_time, prod.startingTime)
                            expected_time = max (time.time()+temp_task_time - self.startingTime, expected_time)
                            if (current_time<0 or current_time < expected_time) and self.stations["C-BS"].outFree and station.inFree and station.IsAvailable():
                                    agent.stepAction = prod.id
                                    current_time=temp_task_time
                                    chosen = 0
                                    if(self.stations["C-BS"].ReadyToGive() and not prod.blocked):
                                        chosen=prod.id
                        
                        else:
                            if not prod.blocked:
                                ind = prod.getPrevJob()[0]
                                prevStationName=self.tasksToStationsName[ind]
                                prevStation = self.stations[prevStationName]
                                temp_task_time= time.time() + self.distances[prevStationName][stationName]/self.speed 
                                temp_task_time += prod.getCurrJob()[1]

                                if (prevStation.GetNextProd() is not None and prevStation.GetNextProd().id == prod.id and (current_time<0 or current_time < temp_task_time) 
                                    and prevStation.outFree and station.inFree and (prod.JobInSameMachine() or 
                                    (prod.LastStation() and not station.GetWaitingProd() or station.IsAvailable()))):
                                    agent.stepAction = prod.id
                                    current_time=temp_task_time
                                    chosen = prod.id

                                elif (not prod.LastStation() and station.GetNextProd() is not None and station.GetNextProd().id==prod.id ):
                                    expected_time=time.time()+prod.RemainingTime()
                                    nJob = prod.getNextJob()
                                    nextStationName = self.tasksToStationsName[nJob[0]]
                                    nextStation = self.stations[nextStationName]
                                    expected_time += self.distances[stationName][nextStationName]/self.speed
                                    expected_time+= nJob[1]
                                    if nextStation.IsAvailable() and (current_time < 0 or current_time < expected_time):
                                        agent.stepAction = prod.id
                                        current_time=temp_task_time
                                        chosen = 0 

        elif chosen2<0 and chosen<0:

            for prod_id in range(1, self.n_products + 1):
                prod = self.products[prod_id]

                if not prod.finished and prod.id not in otherAgentsActions and not prod.grabbed and not prod.assigned:
                    stationName=self.tasksToStationsName[prod.getCurrJob()[0]]
                    station = self.stations[stationName]

                    if (prod.currJobPointer ==0 or(prod.currJobPointer==1 and prod.getNTaskFinished()==0)):
                        temp_task_time = self.computeDist(self.agents_pos[agent_id],self.stations[stationName].getPosition())
                        expected_time = self.stations["C-BS"].ReadyTime()
                        expected_time = max(expected_time, prod.startingTime)
                        expected_time = max (time.time()+temp_task_time - self.startingTime, expected_time)
                        if (current_time<0 or current_time < expected_time) and self.stations["C-BS"].outFree and station.inFree and station.IsAvailable():
                                agent.stepAction = prod.id
                                current_time=temp_task_time
                                chosen = 0
                                if(self.stations["C-BS"].ReadyToGive() and not prod.blocked):
                                    chosen=prod.id
                    
                    else:
                        if not prod.blocked:
                            ind = prod.getPrevJob()[0]
                            prevStationName=self.tasksToStationsName[ind]
                            prevStation = self.stations[prevStationName]
                            temp_task_time= time.time() + self.distances[prevStationName][stationName]/self.speed 
                            temp_task_time += prod.getCurrJob()[1]

                            if (prevStation.GetNextProd() is not None and prevStation.GetNextProd().id == prod.id and (current_time<0 or current_time < temp_task_time) 
                                and prevStation.outFree and station.inFree and (prod.JobInSameMachine() or 
                                (prod.LastStation() and not station.GetWaitingProd()) or station.IsAvailable())):
                                agent.stepAction = prod.id
                                current_time=temp_task_time
                                chosen = prod.id

                            elif (not prod.LastStation() and station.GetNextProd() is not None and station.GetNextProd().id==prod.id ):
                                expected_time=time.time()+prod.RemainingTime()
                                nJob = prod.getNextJob()
                                nextStationName = self.tasksToStationsName[nJob[0]]
                                nextStation = self.stations[nextStationName]
                                expected_time += self.distances[stationName][nextStationName]/self.speed
                                expected_time+= nJob[1]
                                if nextStation.IsAvailable() and (current_time < 0 or current_time < expected_time):
                                    agent.stepAction = prod.id
                                    current_time=temp_task_time
                                    chosen = 0 


        if chosen2>0: return self.n_products+1
        else: return chosen




    def computeDist(self,v1,v2):
        x1, y1 = v1
        x2, y2 = v2
        dx = x2 - x1
        dy = y2 - y1
        
        return math.sqrt(dx**2 + dy**2)

    def ProcessAgentsPos(self,x,y):
        return x,y-4


    def AssignStationsLocations(self,stations_path):

        with open(stations_path, 'r') as file:
            data = json.load(file)  # Load and parse the JSON data
        # Accessing the stations list
        stations_charact = data['stations']
        for item in stations_charact:
            if item["type"]=="BaseStation":
                self.stations["C-BS"].location=(item["position"]["x"],item["position"]["z"])
            
            elif item["type"]=="DeliveryStation":
                self.stations["C-DS"].location=(item["position"]["x"],item["position"]["z"])
                
            elif item["type"]=="RingStation" and item["ringtasks"][0]["taskNumber"][0] in [2,3]:
                self.stations["C-RS1"].location=(item["position"]["x"],item["position"]["z"])
                
            elif item["type"]=="RingStation":
                self.stations["C-RS2"].location=(item["position"]["x"],item["position"]["z"])
                
            elif item["type"]=="CapStation" and item["capTask"]==6:
                self.stations["C-CS1"].location=(item["position"]["x"],item["position"]["z"])
                
            elif item["type"]=="CapStation":
                self.stations["C-CS2"].location=(item["position"]["x"],item["position"]["z"])


    #functions related to the NN observations


    def NormalizeTimeValues(self, time):
        return time /self.maxEpisodeTime
    
    def NormalizeXValues(self,x):
        return (x-self.minX) /(self.maxX - self.minX)
    
    def NormalizeZValues(self,z):
        return (z-self.minZ)/(self.maxZ-self.minZ)
    
    def NormalizeDistanceValues(self,z):
        return z/self.maxD

    def getDistAgentToStation(self,agent_id,stationName):
        temp1=self.agents_pos[agent_id]
        temp2=self.stations[stationName].getPosition()

        return self.computeDist(self.agents_pos[agent_id],self.stations[stationName].getPosition())


    def getObservations(self, agent_id, agent_position):
        observations = []
        self.last_h = self.h
        self.h = 0
        passedTime = time.time() - self.episodeStart
        #timeLastLB = Time.time()
        observations.append(self.NormalizeTimeValues(passedTime))

        null_product_count = 0

        self.add_agent_observations(agent_id, observations)
        self.AddStationsObservations(observations)
        self.add_product_observations(agent_position, observations, null_product_count)

        #self.calculate_and_log_rewards()
        #debug

        # for i in range(len(observations)):
        #     if observations[i]>1:
        #         k=0

        if len(observations)!=345:
             k=0
        return observations



    def AddStationsObservations(self,observations):
        observations.append(self.NormalizeXValues(self.stations["C-RS1"].getPosition()[0]))
        observations.append(self.NormalizeZValues(self.stations["C-RS1"].getPosition()[1]))
        if self.stations["C-RS1"].IsWaitingForComb():
            observations.append(1)
        else:
            observations.append(0)
 
        observations.append(self.NormalizeXValues(self.stations["C-RS2"].getPosition()[0]))
        observations.append(self.NormalizeZValues(self.stations["C-RS2"].getPosition()[1]))
        if self.stations["C-RS2"].IsWaitingForComb():
            observations.append(1)
        else:
            observations.append(0)



    def add_agent_observations(self, agent_id, observations):
        for id,agent in self.agents.items():
            if agent_id ==id:
                observations.append(1)
            else:
                observations.append(0)

            

            #observations.Add(agent.Value.ID / agents.Count);
            #observations.append(id/len(self.agents) )
            observations.append(self.NormalizeXValues(self.agents_pos[agent_id][0]))
            observations.append(self.NormalizeZValues(self.agents_pos[agent_id][1]))
            observations.append(agent.lastAction/13)


    def add_product_observations(self, agent_position, observations, null_product_count):
        for i in range(1, self.n_products + 1):
            n_finished=0
            n_rings = int(self.products[i].n_rings)


            if ((len(observations)-15)%30)!=0:
                k=0

            if self.products[i].finished:
                self.add_null_product_observations(i, self.products[i], observations)
                n_finished+=1
                if n_finished == self.n_products:
                    self.h = self.last_h
            else:
                self.add_non_null_product_observations(i, self.products[i], agent_position, observations)




    def add_null_product_observations(self, index, currProd, observations):
        temp_ob=[]

        observations.append(self.NormalizeTimeValues(currProd.startingTime))
        observations.extend([0, 0, 1, self.NormalizeTimeValues(currProd.getTaskFinishingTime(0))])

        for j in range(1,currProd.n_rings+1):
            id_ = 0.5
            m="C-RS1"
            if self.getStation(currProd.getJob(j)[0])=="C-RS2":
                id_ = 1.0
                m="C-RS2"
            observations.extend([
                1,
                self.NormalizeTimeValues(currProd.getTaskFinishingTime(j)),
                0,
                id_,
                self.taskPayments[currProd.getJob(j)[0]]/3 + 0.1,
                self.NormalizeDistanceValues(self.distances["C-BS"][m])
            ])  


        for j in range(3 - currProd.n_rings):
            observations.extend([0, 0, 0, 0, 0, 0])

        id_c = 0.5
        if int(currProd.getJob(1 + currProd.n_rings)[0]) == 7:
            id_c = 1.0

        observations.extend([
            1, self.NormalizeTimeValues(currProd.getTaskFinishingTime(currProd.n_rings + 1)),0,
            id_c,
            1, self.NormalizeTimeValues(currProd.getTaskFinishingTime(currProd.n_rings + 2)),0
        ])
        k=0



    def add_non_null_product_observations(self, index, currProd, agent_position, observations):
        lower_bound = time.time()-self.episodeStart 



        prodPos=currProd.getCurrPosition()
        distance_to_product_x = prodPos[0] - agent_position[0]
        distance_to_product_z = prodPos[1] - agent_position[1]

        observations.extend([
            self.NormalizeTimeValues(currProd.startingTime),
            self.NormalizeDistanceValues(distance_to_product_x),
            self.NormalizeDistanceValues(distance_to_product_z)
        ])
        # if currProd.currJobPointer ==1 and currProd.getNTaskFinished()==0 or currProd.currJobPointer==0:
        #     observations.append(1)
        # else:
        #     station=currProd.currStation
        #     if station is not None and not currProd.assigned:
        #         nextProd=station.GetNextProd()
        #         if(nextProd is not None and nextProd.id==currProd.id):
        #             observations.append(1)
        #         else:
        #             observations.append(0)
        #     else:
        #         observations.append(0)

        self.add_completed_task_observations(index, currProd, observations)

        if currProd.currJobPointer==0:
             #currentTaskTime=float('inf')
            #lower_bound+=currentTasktime

            expectedTime=self.stations["C-BS"].ReadyTime()
            expectedTime=max(expectedTime,currProd.startingTime)
            lower_bound=max(lower_bound,expectedTime)
            observations.extend([0.5, self.NormalizeTimeValues(lower_bound)])
            if 0 == currProd.n_rings and currProd.n_rings < 3:
                self.AddEmptyTaskObservations(3 - currProd.n_rings, observations)
            self.h = max(self.h, lower_bound)
            self.AddFutureTaskObservations(currProd, lower_bound, observations)


        if (currProd.currJobPointer==1 and currProd.getNTaskFinished()==0):
            #currentTaskTime=float('inf')
            #lower_bound+=currentTasktime
            expectedTime=self.stations["C-BS"].ReadyTime()
            expectedTime=max(expectedTime,currProd.startingTime)
            lower_bound=max(lower_bound,expectedTime)
            observations.extend([0.5, self.NormalizeTimeValues(lower_bound)])
            if 0 == currProd.n_rings and currProd.n_rings < 3:
                    self.AddEmptyTaskObservations(3 - currProd.n_rings, observations)

            dist =self.distances["C-BS"][self.tasksToStationsName[currProd.getCurrJob()[0]]] 
            lower_bound = lower_bound + + self.lbGrabbingTime + self.lbDroppingTime + (dist / self.speed) + currProd.getCurrJob()[1]

            observations.append(0)
            if (currProd.n_rings > 0):

                observations.append(self.NormalizeTimeValues(lower_bound))
                observations.append(self.NormalizeDistanceValues(dist))
                id_=0.5
                m="C-RS1"
                if self.getStation(currProd.getJob(1)[0])=="C-RS2":
                        id_ = 1.0
                        m="C-RS2"

                observations.extend([
                    id_,
                    self.taskPayments[currProd.getJob(1)[0]]/3 + 0.1,
                    self.NormalizeDistanceValues(self.distances["C-BS"][m])
                ])
                if (currProd.n_rings==1):
                    self.AddEmptyTaskObservations(3-currProd.n_rings,observations)

            else:
                observations.append(self.NormalizeTimeValues(lower_bound))
                observations.append(self.NormalizeDistanceValues(dist))
                id_c = 0.5
                if int(currProd.getJob(1 + currProd.n_rings)[0]) == 7:
                    id_c = 1.0
                observations.append(id_c)

            self.AddFutureTaskObservations(currProd, lower_bound, observations)
        if currProd.currJobPointer>0 and currProd.getNTaskFinished()>0 and currProd.currJobPointer < len(currProd.jobs) and not currProd.finished:

            if not currProd.blocked:
                observations.append(0.5)
                lower_bound = self.calculate_current_task_time(currProd, lower_bound, observations)

            else:
                if not currProd.processing and currProd.currJobPointer!=0:
                    lower_bound += currProd.getCurrJob()[1]
                else:
                    lower_bound += currProd.getCurrJob()[1] - (time.time() - currProd.processStart)  # Adjust to your time function
                
                observations.extend([1, self.NormalizeTimeValues(lower_bound),
                    self.NormalizeDistanceValues(self.distances[self.tasksToStationsName[currProd.getPrevJob()[0]]][self.tasksToStationsName[currProd.getCurrJob()[0]]])])

            self.add_task_pointer_observations(currProd, lower_bound, observations)

            if currProd.currJobPointer == currProd.n_rings and currProd.n_rings < 3:
                self.AddEmptyTaskObservations(3 - currProd.n_rings, observations)

            self.h = max(self.h, lower_bound)
            self.AddFutureTaskObservations(currProd, lower_bound, observations)

    def add_completed_task_observations(self, index, currProd, observations):
        for i in range(len(currProd.jobsFinishingTime)):
            if currProd.jobsFinishingTime[i]>0:
                observations.extend([1, self.NormalizeTimeValues(currProd.jobsFinishingTime[i])])
                if i!=0:
                    observations.append(0)
                if 1 <= i <= 3 and i <= currProd.n_rings:
                    id_ = 0.5
                    m="C-RS1"
                    if self.getStation(currProd.getJob(i)[0])=="C-RS2":
                        id_ = 1.0
                        m="C-RS2"

                    observations.extend([
                        id_,
                        self.taskPayments[currProd.getJob(i)[0]]/3 + 0.1,
                        self.NormalizeDistanceValues(self.distances["C-BS"][m])
                    ])

                if i == currProd.n_rings and currProd.n_rings < 3:
                    self.AddEmptyTaskObservations(3 - currProd.n_rings, observations)
                elif i == currProd.n_rings + 1:
                    id_c = 0.5
                    if int(currProd.getJob(1 + currProd.n_rings)[0]) == 7:
                        id_c = 1.0
                    observations.append(id_c)
            else: 
                break

    def calculate_current_task_time(self, currProd, lower_bound,observations):
        # Replace this with your pathfinding code

        if currProd.currAgent is not None and currProd.currAgent.grabingProd is not None:
            dist=self.distances[self.tasksToStationsName[currProd.getPrevJob()[0]]][self.tasksToStationsName[currProd.getCurrJob()[0]]]
            lower_bound = (lower_bound + currProd.currAgent.timeGrabbing -(time.time()- currProd.currAgent.startingTimeGrabbing) +
                            self.lbDroppingTime + dist/self.speed + currProd.getCurrJob()[1])
            observations.append(self.NormalizeTimeValues(lower_bound))
            observations.append(self.NormalizeDistanceValues(dist))

        elif currProd.currAgent is not None and currProd.currAgent.dropingProd is not None:
            lower_bound = (lower_bound + currProd.currAgent.timeDropping -
                           (time.time()- currProd.currAgent.startingTimeDropping) + currProd.getCurrJob()[1])
            observations.append(self.NormalizeTimeValues(lower_bound))
            observations.append(0)

        elif currProd.currAgent :
            dist = self.getDistAgentToStation(currProd.currAgent.id,self.tasksToStationsName[currProd.getCurrJob()[0]])
            lower_bound = (lower_bound + self.lbDroppingTime + dist/self.speed + currProd.getCurrJob()[1])
            observations.append(self.NormalizeTimeValues(lower_bound))
            observations.append(self.NormalizeDistanceValues(dist))

        elif currProd.assigned :
            dist=self.distances[self.tasksToStationsName[currProd.getPrevJob()[0]]][self.tasksToStationsName[currProd.getCurrJob()[0]]]
            lower_bound = (lower_bound + self.lbGrabbingTime +
                            self.lbDroppingTime + dist/self.speed + currProd.getCurrJob()[1])
            observations.append(self.NormalizeTimeValues(lower_bound))
            observations.append(self.NormalizeDistanceValues(dist))

        else:
            dist=self.distances[self.tasksToStationsName[currProd.getPrevJob()[0]]][self.tasksToStationsName[currProd.getCurrJob()[0]]]
            lower_bound = (lower_bound + self.lbGrabbingTime +
                            self.lbDroppingTime + dist/self.speed + currProd.getCurrJob()[1])
            observations.append(self.NormalizeTimeValues(lower_bound))
            observations.append(self.NormalizeDistanceValues(dist))
        
        return lower_bound 

    def add_task_pointer_observations(self, currProd, lower_bound, observations):
        if 1 <= currProd.currJobPointer <= 3 and currProd.currJobPointer <= currProd.n_rings:
            id_ = 0.5
            m="C-RS1"
            if self.getStation(currProd.getJob(currProd.currJobPointer)[0])=="C-RS2":
                id_ = 1.0
                m="C-RS2"

            observations.extend([
                id_,
                self.taskPayments[currProd.getJob(currProd.currJobPointer)[0]]/3 + 0.1,
                self.NormalizeDistanceValues(self.distances["C-BS"][m])
            ])

        if currProd.currJobPointer == currProd.n_rings + 1:
            id_c = 0.5
            if int(currProd.getJob(1 + currProd.currJobPointer)[0]) == 7:
                id_c = 1.0
            observations.append(id_c)


    def AddEmptyTaskObservations(self, count, observations):
        for _ in range(count):
            observations.extend([0, 0, 0, 0, 0, 0])

    def AddFutureTaskObservations(self, currProd, lowerBound,  observations):
        for k in range(currProd.currJobPointer+ 1, len(currProd.jobs)):
            dist=self.distances[self.tasksToStationsName[currProd.getJob(k-1)[0]]][self.tasksToStationsName[currProd.getJob(k)[0]]]
            lowerBound = lowerBound + (dist / self.speed) + currProd.getJob(k-1)[1]
            observations.extend([0, self.NormalizeTimeValues(lowerBound), self.NormalizeDistanceValues(dist)])
            
            if 1 <= k <= 3 and k <= currProd.n_rings:
                id_ = 0.5
                m="C-RS1"
                if self.getStation(currProd.getJob(k)[0])=="C-RS2":
                    id_ = 1.0
                    m="C-RS2"

                dist=self.NormalizeDistanceValues(self.distances["C-BS"][m])

                observations.extend([
                    id_,
                    self.taskPayments[currProd.getJob(k)[0]]/3 + 0.1,
                    self.NormalizeDistanceValues(self.distances["C-BS"][m])
                ])
            self.h = max(self.h, lowerBound)
            
            if k == currProd.n_rings and currProd.n_rings < 3:
                self.AddEmptyTaskObservations(3 - currProd.n_rings, observations)
                
            if k == currProd.n_rings + 1:
                id_c = 0.5
                if int(currProd.getJob(k)[0]) == 7:
                    id_c = 1.0
                observations.append(id_c)


    def CalculateAndLogRewards(self):
        self.reward = self.last_h - self.h
        self.cummulativeReward += self.reward
        
        if self.verbose:
            print(f"reward: {self.reward}")
            print(f"cummulativeReward: {self.cummulativeReward}")
        
        if -550 < self.cummulativeReward < -500:
            self.DLogs()

    # def getObservations(self,agentID):
    #     observations=[]

    #     for agent in self.agents:
    #         if(agent.id is not agentID):
    #             observations.append(agent.lastAction)
        
    #     for i in range(self.n_products):
    #         currProd=self.products[i+1]
    #         observations.append(currProd.startingTime)
    #         t=self.getDistanceToMachine() #to correctly implement
    #         observations.append(t[0])
    #         observations.append(t[1])
    #         n_finished=len(currProd.jobsFinishingTime)
            
    #         for j in range(n_finished):
    #             observations.append(1)
    #             observations.append(currProd.jobsFinishingTime[j])
    #             if j>=1 and j<=3 and j<=currProd.n_rings:
    #                 observations.append()

    #             if currProd.n_rings<3 and j==1+currProd.n_rings:
    #                 for k in range(3-currProd.n_rings):
    #                     observations.append(0)
    #                     observations.append(0)
    #                     observations.append(0)
    #                     observations.append(0)
                
    #         currLB=self.starting_time-time.time()

    #         for j in range(n_finished,len(currProd.jobs)):
    #             if currProd.n_rings<3 and j==1+currProd.n_rings:
    #                 for k in range(3-currProd.n_rings):
    #                     observations.append(0)
    #                     observations.append(0)
    #                     observations.append(0)
    #                     observations.append(0)

    #             if j==0:
    #                 currLB+=random.uniform(10, 30)
    #                 observations.append(0)
    #                 observations.append(currLB)

    #             else:
    #                 currLB+=self.computeLowerbound(random.uniform(10,30),
    #                                                self.tasksToRealStations(currProd.jobs[currProd.currJobPointer-1]),
    #                                                self.tasksToRealStations(currProd.jobs[currProd.currJobPointer]))
    #                 observations.append(0)
    #                 observations.append(currLB)


    #     return observations
#end of functions related to the observations

    def CheckDeadlock(self):
        temp_p= self.stations["C-RS1"].GetNextProd();
        deadlockProds= [-1]*2
        if temp_p is not None and not temp_p.grabbed and not temp_p.assigned:
            next = int( temp_p.getCurrJob()[0])
            if self.tasksToStationsName[next] != "C-RS2" or self.stations["C-RS1"].nProds()<2:
                return False
            else:
                deadlockProds[0]=temp_p.id
        else: 
            return False
        
        temp_p= self.stations["C-RS2"].GetNextProd()
        if temp_p is not None and not temp_p.grabbed and not temp_p.assigned:
            next = int( temp_p.getCurrJob()[0])
            if self.tasksToStationsName[next] != "C-RS1" or self.stations["C-RS2"].nProds()<2:
                return False
            else:
                deadlockProds[1]=temp_p.id
        else:
            return False
        
        self.deadlockProds=deadlockProds
        
        self.deadlock = True
        return True


    def getStation(self,task):
        return self.stations[self.tasksToStationsName[task]]

    def InitProducts(self):
        
        with open(self.config["product_path"], 'r') as file:
            data = json.load(file)

        id=1
        for prod in data["products"]:
            jobs = [(1,random.uniform(4,7))]
            n_rings = 0
            for item in prod["ringElements"]:
                jobs.append((item["taskNumber"],random.uniform(20,25)))
                n_rings += 1
            
            jobs.append((prod["capElement"],random.uniform(20,25)))
            jobs.append((8,random.uniform(5,8)))
            p=Product(startingTime=prod["startingTime"],startingStation=self.stations["C-BS"],taskToStationsName=self.tasksToStationsName, jobs=jobs, n_rings=n_rings, id=id,episodeStart=self.episodeStart)
            #self.stations["C-BS"].ReceiveProduct(produ)
            self.products[p.id] = p
            id+=1
            self.stations["C-BS"].ReceiveProduct(p)


    def JobInSameStation(self,task1,task2):
        if self.tasksToStationsName[task1]==self.tasksToStationsName[task2]:
            return True
        return False


    def getMask(self,agent_id):

        mask=[1]*(self.n_products+2)
        mask[self.n_products+1]=0

        all_ready = True
        one_true = False
        prod_wst = False
        deadlock_prio = False
        zeros = 1

        if(not self.agents[agent_id].decided):
            for prod_id in range(1, self.n_products + 1):
                prod = self.products[prod_id]
                #temp=self.tasksToStationsName[int(prod.getCurrJob()[0])]
                station = self.stations[self.tasksToStationsName[int(prod.getCurrJob()[0])]]

                if not prod_wst and prod.startingTime> time.time() - self.startingTime:
                    prod_wst = True  

                if not prod.finished and prod.processing and station.GetReadyProd is not None:
                    all_ready = False

                temp=prod.currJobPointer
                if not prod.finished and prod.currJobPointer>0:
                    stationName=self.tasksToStationsName[prod.getCurrJob()[0]]
                    station = self.stations[stationName]
                    prevStationName=self.tasksToStationsName[prod.getPrevJob()[0]]
                    prevStation = self.stations[prevStationName]
                    if not prod.blocked and prod.currStation!=None and prevStationName!=prod.currStation.name:
                        k=0

                    if(prevStationName!="C-BS" and prevStation.GetNextProd()is not None and prevStation.GetNextProd().id!=prod_id):
                        mask[prod_id]=0
                        zeros+=1
                    
                    elif (not prod.blocked and not prod.grabbed and not prod.assigned and 
                        (self.deadlock or self.CheckDeadlock()) and 
                        station.inFree and prevStation.outFree):
                        founded=False
                        for i in range(len(self.deadlockProds)):
                            if self.deadlockProds[i]==prod.id:
                                founded=True
                                break

                        if ((stationName[:4]=="C-RS") and (prevStationName[:4]=="C-RS") and stationName != prevStationName and
                            not founded):
                            mask[prod_id]=0
                            zeros += 1
                        elif ((stationName[:4]=="C-RS") and (prevStationName[:4]=="C-RS") and stationName != prevStationName and 
                            (station.IsWaitingForComb() or not station.WaitingAvailable())):
                            mask[prod_id]=0
                            zeros += 1
                        elif stationName[:4]=="C-RS" and prevStationName[:4] != "C-RS":
                            mask[prod_id]=0
                            zeros += 1
                        elif stationName[:4]=="C-RS" and (not station.IsAvailable() or not station.inFree or prod.processing) and not founded:
                            mask[prod_id]=0
                            zeros += 1

                    elif (not prod.finished and 
                        ((stationName!='C-DS' and  not station.IsAvailable() and 
                            (not prod.JobInSameMachine() or station.GetWaitingProd())) or 
                        not station.inFree or not prevStation.outFree or prod.processing )):
                        mask[prod_id]=0
                        zeros += 1

                else:
                    mask[prod_id]=0
                    zeros += 1

        if (not deadlock_prio and not self.stations["C-BS"].blocked and 
            self.stations["C-BS"].outFree and self.stations["C-BS"].ReadyToGive()):

            if (self.stations["C-RS2"].IsWaitingForComb() and self.stations["C-RS2"].inFree) or (self.stations["C-RS1"].IsWaitingForComb() and self.stations["C-RS1"].inFree):
                mask[self.n_products+1]=1 
                zeros-=1
                one_true=True

        if  zeros != self.n_products + 1 and not prod_wst: #all_ready and
            mask[0]=0
        # mask=[1]*13
        # mask[0]=0
        return mask


   
    def DeadlockResolved(self):
        self.deadlock=False

    def computeLowerbound(self,processTime,machine1,machine2):
        return (processTime + self.m_distances[machine1][machine2])/self.a_speed

    def getDistanceToMachine():
        return (0,0)


    def onActionReceived(self, action,agent_id):
        
        action1 = action - self.n_products
        ag=self.agents[agent_id]
        if not ag.decided:
            if action > 0 and action1 <= 0:
                prod = self.products[action]

                if not prod.finished and not prod.assigned and not prod.blocked and not prod.grabbed and prod.currJobPointer > 0:
                    stationName=self.tasksToStationsName[prod.getCurrJob()[0]]
                    station = self.stations[stationName]
                    prevStationName=self.tasksToStationsName[prod.getPrevJob()[0]]
                    prevStation = self.stations[prevStationName]


                    if self.deadlock and station.inFree and prevStation.outFree:
                        if stationName[:4]=="C-RS":
                            
                            onlyOne=0
                            indD=-1
                            for i in range(len(self.deadlockProds)):
                                val=self.deadlockProds[i]
                                if val>0:
                                    onlyOne+=1
                                    if val==prod.id:
                                        indD=i

                            if indD>-1 and prevStation.name[:4]=="C-RS" and station.name != prevStation.name and station.WaitingAvailable():
                                # onlyOne = sum(1 for kvp in controller.get_ring_stations().values()
                                #             if kvp.get_next_prod() and not kvp.get_next_prod().blocked and not kvp.get_next_prod().assigned)
                                # if only_one == 1:
                                #     self.DeadlockResolved()
                                self.deadlockProds[indD]=-1
                                if(onlyOne==1):
                                    self.deadlock=False
                                temp=prod.getCurrJob()[0]
                                payments=self.taskPayments[int(prod.getCurrJob()[0])]
                                station.AssignInTask(payments)
                                prevStation.AssignOutTask()
                                prod.assigned = True
                                job_product = action
                                nextJob=self.products[action].getCurrJob()
                                self.products[action].available=False
                                
                                self.agents[agent_id].AssignAction(nextJob,action,prod,outStation=prevStation,inStation=station)


                            elif prevStation.name[:4]=="C-RS" and station.WaitingAvailable():
                                payments=self.taskPayments[int(prod.getCurrJob()[0])]
                                temp=prod.getCurrJob()[0]
                                station.AssignInTask(payments)
                                prevStation.AssignOutTask()
                                prod.assigned = True
                                job_product = action
                                nextJob=self.products[action].getCurrJob()
                                self.products[action].available=False
                                
                                self.agents[agent_id].AssignAction(nextJob,action,prod,outStation=prevStation,inStation=station)

                        elif station.IsAvailable() and station.inFree and prevStation.outFree and (prod.currJobPointer > 1 or self.stations["C-BS"].ReadyToGive()):
                            payments=self.taskPayments[int(prod.getCurrJob()[0])]
                            temp=prod.getCurrJob()[0]
                            station.AssignInTask(payments)
                            prevStation.AssignOutTask()
                            prod.assigned = True
                            job_product = action
                            nextJob=self.products[action].getCurrJob()
                            self.products[action].available=False
                            
                            if prod.currJobPointer == 1:
                                prod.AddFinishingTime(t=self.stations["C-BS"].preparationFinished(),ind=0)

                            self.agents[agent_id].AssignAction(nextJob,action,prod,outStation=prevStation,inStation=station)

                                
                        elif not self.agents[agent_id].inInit and not self.agents[agent_id].assignedToInit:
                            self.assignToInitPos(agent_id)

                    elif self.CheckDeadlock() and station.inFree and prevStation.outFree:

                        if stationName.name[:4]=="C-RS":
                            onlyOne=0
                            indD=-1
                            for i in range(len(self.deadlockProds)):
                                val=self.deadlockProds[i]
                                if val>0:
                                    onlyOne+=1
                                    if val==prod.id:
                                        indD=i

                            if indD >-1 and prevStation.name[:4]=="C-RS" and station.name != prevStation.name and station.WaitingAvailable():

                                # only_one = sum(1 for kvp in controller.get_ring_stations().values()
                                #             if kvp.get_next_prod() and not kvp.get_next_prod().blocked and not kvp.get_next_prod().assigned)
                                # if only_one == 1:
                                #     controller.deadlock_resolved()
                                self.deadlockProds[indD]=-1
                                if onlyOne==1:
                                    self.deadlock=False
                                payments=self.taskPayments[int(prod.getCurrJob()[0])]
                                temp=prod.getCurrJob()[0]
                                station.AssignInTask(payments)
                                prevStation.AssignOutTask()
                                prod.assigned = True
                                job_product = action
                                nextJob=self.products[action].getCurrJob()
                                self.products[action].available=False
                                
                                self.agents[agent_id].AssignAction(nextJob,action,prod,outStation=prevStation,inStation=station)



                            elif prevStation.name[:4]=="C-RS" and station.WaitingAvailable():
                                temp=prod.getCurrJob()[0]
                                payments=self.taskPayments[int(prod.getCurrJob()[0])]
                                station.AssignInTask(payments)
                                prevStation.AssignOutTask()
                                prod.assigned = True
                                job_product = action
                                nextJob=self.products[action].getCurrJob()
                                self.products[action].available=False
                                
                                self.agents[agent_id].AssignAction(nextJob,action,prod,outStation=prevStation,inStation=station)


                        elif station.is_available() and station.inFree and prevStation.outFree and (prod.task_pointer > 1 or self.stations["C-BS"].ReadyToGive()):
                            temp=prod.getCurrJob()[0]
                            payments=self.taskPayments[int(prod.getCurrJob()[0])]
                            station.AssignInTask(payments)
                            prevStation.AssignOutTask()
                            prod.assigned = True
                            job_product = action
                            nextJob=self.products[action].getCurrJob()
                            self.products[action].available=False
                            
                            if prod.task_pointer == 1:
                                prod.AddFinishingTime(t=self.stations["C-BS"].preparationFinished(),ind=0)

                            self.agents[agent_id].AssignAction(nextJob,action,prod,outStation=prevStation,inStation=station)

                                
                        elif not self.agents[agent_id].inInit and not self.agents[agent_id].assignedToInit:
                            self.assignToInitPos(agent_id)




                    elif (( (prod.JobInSameMachine() and station.WaitingAvailable()) or station.IsAvailable()) and station.inFree and prevStation.outFree 
                        and (prod.currJobPointer > 1 or self.stations["C-BS"].readyToGive)):
                    
                        payments=0
                        task=int(prod.getCurrJob()[0])
                        payments=self.taskPayments[int(prod.getCurrJob()[0])]
                        temp=prod.getCurrJob()[0]
                        station.AssignInTask(payments)
                        prevStation.AssignOutTask()
                        prod.assigned = True
                        nextJob=self.products[action].getCurrJob()
                        #self.products[action].available=False
                        
                        if prod.currJobPointer == 1:
                            t=self.stations["C-BS"].preparationFinished
                            prod.AddFinishingTime(t,ind=0)

                        self.agents[agent_id].AssignAction(nextJob,action,prod,outStation=prevStation,inStation=station)


                    elif not self.agents[agent_id].inInit and not self.agents[agent_id].assignedToInit:
                        self.assignToInitPos(agent_id)
                elif not self.agents[agent_id].inInit and not self.agents[agent_id].assignedToInit:
                    self.assignToInitPos(agent_id)

            elif action1 > 0:


                if self.stations["C-RS1"].IsWaitingForComb() and self.stations["C-BS"].outFree: #and self.stations["C-RS1"].inFree 
                    self.stations["C-RS1"].WillReceive(1)
                    self.agents[agent_id].AssignCombAction(self.stations["C-RS1"])

                elif self.stations["C-RS2"].IsWaitingForComb() and self.stations["C-BS"].outFree:
                    self.stations["C-RS2"].WillReceive(1)
                    self.agents[agent_id].AssignCombAction(self.stations["C-RS2"])


            elif not self.agents[agent_id].inInit and not self.agents[agent_id].assignedToInit:
                self.assignToInitPos(agent_id)


        # elif not self.agents[agent_id].inInit and not self.agents[agent_id].assignedToInit:
        #     self.agents[agent_id].decided=False
        #     self.assignToInitPos(agent_id)

        self.agents[agent_id].last_action = action





    
    # def performAction(self,action,agent_id):

    #     action1=action-self.n_products
    #     if action>0 and action1<=0:
    #         job=self.products[action].getPrevJob()
    #         next_job=self.products[action].getCurrJob()
    #         self.products[action].available=False
    #         station_out = self.getStation(task=job[0])
    #         station_in = self.getStation(task=next_job[0])
    #         if next_job[0]<6 and next_job[0]>1:
    #             station_in.combRequired=self.taskPayments[next_job[0]]
            
    #         if station_out.outFree:

    #             station_in.willHave = action
    #             #station_in.inFree=False
    #             station_out.outFree=False

    #             self.agents[agent_id].currJob=next_job
    #             self.agents[agent_id].goingToProd=action
    #             self.agents[agent_id].currProd=self.products[action]
    #             self.agents[agent_id].firstOrder=(station_out.name,"OUTPUT")
    #             self.agents[agent_id].secondOrder=(station_in.name,"INPUT")
    #             self.agents[agent_id].available=False
            
    #     elif action1>0:
    #         if self.stations["C-RS1"].IsWaitingForComb() and self.stations["C-BS"].outFree: #and self.stations["C-RS1"].inFree 
    #             self.agents[agent_id].available=False
    #             self.agents_proto[agent_id].combTo="C-RS1"

    #         elif self.stations["C-RS2"].IsWaitingForComb() and self.stations["C-BS"].outFree:
    #             self.agents[agent_id].available=False
    #             self.agents_proto[agent_id].combTo="C-RS2"

        

    # def performAction2(self,action,agent_id):

    #     action1=action-self.n_products
    #     if action>0 and action1<=0:
    #         job=self.products[action].getNextJob()
    #         station = self.getStation(task=job[0])
    #         if station.available:
    #             self.agents_proto[agent_id].reset_task()
    #             self.agents_proto[agent_id].set_move_action(station.name,"OUTPUT")
    #             station.available=False
    #             self.agents[agent_id].available=False
    #             self.agents[agent_id].goingToProd=action
    #             serialized_msg=self.agents_proto[agent_id].serialize_task()
    #             env.message.SendMsg(serialized_msg)
    #             self.agents[agent_id].available=False
    #             self.agents[agent_id].waitingForAnswer=True
            
    #     elif action1>0:
    #         if self.stations["C-BS"].available and self.stations["C-RS1"].IsWaitingForComb():
    #             self.agents_proto[agent_id].reset_task()
    #             self.agents_proto[agent_id].set_move_action("C-BS","OUTPUT")
    #             self.stations["C-BS"].available=False
    #             self.agents[agent_id].available=False
    #             self.agents[agent_id].goingToCombProd=True
    #             serialized_msg=self.agents_proto[agent_id].serialize_task()
    #             env.message.SendMsg(serialized_msg)
    #             self.agents[agent_id].available=False
    #             self.agents[agent_id].waitingForAnswer=True
        
    #     elif action==0:
    #         self.agents_proto[agent_id].reset_task()
    #         self.agents_proto[agent_id].move_to_init_pos()
    #         serialized_msg=self.agents_proto[agent_id].serialize_task()
    #         env.message.SendMsg(serialized_msg)
    #         self.agents[agent_id].available=False
    #         self.agents[agent_id].waitingForAnswer=True


    def CheckIfOrderPossible(self,order):
        if self.stations[order[0]].available:
            return True
        return False
    
    def assignToInitPos(self,agent_id):    
        # self.agents_proto[agent_id].move_to_init_pos()
        # serialized_msg=self.agents_proto[agent_id].serialize_task()
        # env.message.SendMsg(serialized_msg)
        # print(f"agent {agent_id} is assigned to go to its init position")
        
        # self.agents[agent_id].finishedOrder=False
        self.agents[agent_id].decided=True
        print(f"agent {agent_id} is ordered to go to init Pos")
        self.agents[agent_id].assignedToInit=True
        

    # def ReceivedMsg(self,msg_proto):
    #     id=msg_proto.robot_id
    #     ag=self.agents[id]
    #     ag.ReceiveMsg(msg_proto)

    def InitMsg(self,sock,ip,port):
        self.message=Msg(sock=sock,broadcast_ip=ip,port=port)


    def AssignAction(self):
        
        for agent_id, agent in self.agents.items():  
            if not agent.decided :
                action =0
                if self.nn:
                    obs = self.getObservations(agent_id,agent.getCurrPosition())
                    mask = self.getMask(agent_id)
                    input_data = {
                        'obs_0': obs,
                        'action_masks': mask
                        }

                    action = self.model.run_inference(input_data)
                    discrete_actions = action[0]  # Assuming this is the discrete actions output
                    if discrete_actions.size == 1:
                        action = int(discrete_actions.item())
                    else:
                        action = int(discrete_actions[0][0]) 
                    if action>0:
                        k=0
                elif self.spt:
                    action =self.SPT(agent_id=agent_id)
                else:
                    action =self.LPT(agent_id=agent_id)
                self.onActionReceived(action,agent_id)
                if self.verbose:
                    self.log+=f"Action {action} was assigned to agent {agent_id} at time {time.time()-self.episodeStart} \n "
                #store action check if machine available otherwise stock action and wait


    # def continueAction(self, agent_id):
    #     # self.agents[agent_id].GrabProduct()
    #     # prevTaskID=self.agents[agent_id].currJob[0]
    #     # self.agents[agent_id].lastAction=prevTaskID
    #     # currProd=self.products[self.agents[agent_id].haveProd]
    #     self.stations[self.agents[agent_id].firstOrder[0]].available=True # to change 
    #     # self.agents[agent_id].nextOrder=( self.tasksToStationName[currProd.getCurrJob()[0]],"OUTPUT")

    def ProcessMsg(self,serialized_msg,address):
        
        #print(serialized_msg)
        msg = self.agents_proto[1].process_msg(serialized_msg)
        msg2= self.robot_info.process_robot_msg(serialized_msg)
        #time.sleep(0.001)
        if msg.successful==True : #msg.CompType == self.agents_proto[2].CompType["MSG_TYPE"]:
            if not self.agents_proto[msg.robot_id].checkIfAlreadyProcessed(msg):
                self.agents_proto[msg.robot_id].setLastReceived(msg)
                print(f"Message related to agent {self.agent_ips[address[0]]} Received")
                task_id=msg.task_id
                print("action was successfull")
                self.agents[msg.robot_id].finishedOrder=True
                self.agents[msg.robot_id].currLocation=(msg.move.waypoint,msg.move.machine_point)
                # k=msg.order_id
                # if k>1:
                #     k=0
                if msg.move.waypoint==self.agents[msg.robot_id].init_pos[0] and msg.move.waypoint==self.agents[msg.robot_id].init_pos[0]:
                    print(f"agent {msg.robot_id} reached its init Position")
                    self.agents[msg.robot_id].assignedToInit=False
                    self.agents[msg.robot_id].inInit=True
                    self.agents[msg.robot_id].decided=False
                    self.agents[msg.robot_id].finishedOrder=True
                elif msg.move.machine_point=="OUTPUT":
                    if self.agents[msg.robot_id].goingToProd>0:
                        print(f"agent {msg.robot_id} reached Machine {msg.move.waypoint} - {msg.move.machine_point} to grab {self.agents[msg.robot_id].goingToProd}")
                        #self.agents[msg.robot_id].processing=False
                        self.agents[msg.robot_id].GrabProduct(self.products[self.agents[msg.robot_id].goingToProd])
                        #self.continueAction(msg.robot_id)
                        #self.agents[msg.robot_id].receiveProduct()
                        #do next action
                    elif msg.move.waypoint=="C-BS" and self.agents[msg.robot_id].combTo !="":
                        print(f"agent {msg.robot_id} reached Machine C-BS - {msg.move.machine_point} to grab a combProduct")
                        self.agents[msg.robot_id].GrabCombProduct()


                elif msg.move.machine_point=='INPUT':
                    self.agents[msg.robot_id].available=True
                    #self.agents[msg.robot_id].processing=False

                    if self.agents[msg.robot_id].haveProd>0:
                        print(f"agent {msg.robot_id} reached Machine {msg.move.waypoint} - {msg.move.machine_point} to drop {self.agents[msg.robot_id].haveProd}")
                        
                        self.agents[msg.robot_id].DropProduct()
                        #n_combProd=self.taskPayments[self.products[self.agents[msg.robot_id].lastAction].getCurrJob()[0]]
                        #self.stations[msg.move.waypoint].ReceiveProduct(self.products[self.agents[msg.robot_id].lastAction]) #self.agents[msg.robot_id],

                    elif self.agents[msg.robot_id].combTo !='':
                        print(f"agent {msg.robot_id} reached Machine {msg.move.waypoint} - {msg.move.machine_point} to drop a combProduct ")
                        self.agents[msg.robot_id].lastAction=self.n_products+1
                        self.agents[msg.robot_id].DropCombProduct()
                        # self.agents[msg.robot_id].combTo=None
                        # self.stations[msg.move.waypoint].ReceiveCombProd()

        elif msg.successful==False  and msg2.name != "3" and msg2.name != "2" and msg2.name != "1":
            #if not self.agents_proto[msg.robot_id].checkIfAlreadyProcessed(msg):
            time.sleep(0.000001)
            print("action wasn't successfull redo it")
            task_id=msg.task_id
            #self.first_failure+=1
            if  self.agents_proto[msg.robot_id].failureSolved(time.time()): #self.first_failure==1
                self.agents_proto[msg.robot_id].SetFailureStart(time.time())
                serialized_msg = self.agents_proto[msg.robot_id].redo_after_failure_last_action()
                self.agents[msg.robot_id].finishedOrder=False
                #serialized_msg = self.agents_proto[msg.robot_id].serialize_task()
                self.message.SendMsg(serialized_msg)  
                print(f"agent {msg.robot_id} is assigned to move again to machine {msg.move.waypoint} - {msg.move.machine_point}") 
            elif self.first_failure==2:
                self.first_failure=0
        elif msg2.name == "3" or msg2.name == "2" or msg2.name == "1":
            x,y=self.ProcessAgentsPos(msg2.pose.x,msg2.pose.y)
            self.agents_pos[int(msg2.name)]=(x,y)

    def ProcessMsg2(self,serialized_msg):

        msg = self.agents_proto[2].process_msg(serialized_msg)
        time.sleep(0.001)
        if msg.successful==True:
            print(f"Message related to agent {self.agent_ips[address[0]]} Received") 
            print("action was successfull")
            task_id=msg.task_id
            #if task_id==4:
            self.agents[msg.robot_id].finishedOrder=True
            self.agents[msg.robot_id].currLocation=(msg.move.waypoint,msg.move.machine_point)
            if msg.move.waypoint==self.agents[msg.robot_id].init_pos[0] and msg.move.waypoint==self.agents[msg.robot_id].init_pos[0]:
                print(f"agent {msg.robot_id} reached its init Position")
                self.agents[msg.robot_id].assignedToInit=False
                self.agents[msg.robot_id].inInit=True
                self.agents[msg.robot_id].decided=False
                self.agents[msg.robot_id].finishedOrder=True
            elif msg.move.machine_point=="OUTPUT":
                if self.agents[msg.robot_id].goingToProd>0:
                    print(f"agent {msg.robot_id} reached Machine {msg.move.waypoint} - {msg.move.machine_point} to grab {self.agents[msg.robot_id].goingToProd}")
                    #self.agents[msg.robot_id].processing=False
                    self.agents[msg.robot_id].GrabProduct(self.products[self.agents[msg.robot_id].goingToProd])
                    #self.continueAction(msg.robot_id)
                    #self.agents[msg.robot_id].receiveProduct()
                    #do next action
                elif msg.move.waypoint=="C-BS" and self.agents[msg.robot_id].combTo !="":
                    print(f"agent {msg.robot_id} reached Machine C-BS - {msg.move.machine_point} to grab a combProduct")
                    self.agents[msg.robot_id].GrabCombProduct()


            elif msg.move.machine_point=='INPUT':
                self.agents[msg.robot_id].available=True
                #self.agents[msg.robot_id].processing=False

                if self.agents[msg.robot_id].haveProd>0:
                    print(f"agent {msg.robot_id} reached Machine {msg.move.waypoint} - {msg.move.machine_point} to drop {self.agents[msg.robot_id].haveProd}")
                    
                    self.agents[msg.robot_id].DropProduct()
                    #n_combProd=self.taskPayments[self.products[self.agents[msg.robot_id].lastAction].getCurrJob()[0]]
                    #self.stations[msg.move.waypoint].ReceiveProduct(self.products[self.agents[msg.robot_id].lastAction]) #self.agents[msg.robot_id],

                elif self.agents[msg.robot_id].combTo !='':
                    print(f"agent {msg.robot_id} reached Machine {msg.move.waypoint} - {msg.move.machine_point} to drop a combProduct ")
                    self.agents[msg.robot_id].lastAction=self.n_products+1
                    self.agents[msg.robot_id].DropCombProduct()
                    # self.agents[msg.robot_id].combTo=None
                    # self.stations[msg.move.waypoint].ReceiveCombProd()
            #in case of failure?
            #else:
        #elif msg.task_id==4:
        print(f"Message related to agent {self.agent_ips[address[0]]} Received") 
        time.sleep(0.0001)
        print("action wasn't successfull redo it")
        task_id=msg.task_id
        self.first_failure+=1
        if self.first_failure==1:
            self.agents_proto[msg.robot_id].redo_last_action()
            self.agents[msg.robot_id].finishedOrder=False
            serialized_msg = self.agents_proto[msg.robot_id].serialize_task()
            self.message.SendMsg(serialized_msg)  
            print(f"agent {msg.robot_id} is assigned to move again to machine {msg.move.waypoint} - {msg.move.machine_point}") 
        elif self.first_failure==2:
            self.first_failure=0


                

    def CheckIfSameMachine(self,agent_id,task_id):
        if self.stations[self.tasksToStationsName[task_id]]==self.agents[agent_id]:
            return True
        return False

    def CancelAgentAction(self,id):
        self.agents[id].assignedToInit=False
        self.agents_proto[id].reset_task()
        self.agents_proto[id].cancel_action()
        serialized_msg = self.agents_proto[id].serialize_task()
        print(f"agent {id} got action canceled ")
        self.message.SendMsg(serialized_msg)   

                


    def CheckForAgent(self,id,agent):

        first,second= agent.getCurrOrder()
        if agent.decided and agent.finishedOrder and first != "":
            if second=="OUTPUT" and agent.goingToProd>0 and not agent.startedGrabbing:#agent.grabingProd==None
                currStation,stationPos=agent.currLocation
                if currStation != "":
                    if stationPos=="OUTPUT" and currStation != agent.init_pos[0]:
                        self.stations[currStation].outFree=True
                    elif currStation != agent.init_pos[0]:
                        self.stations[currStation].inFree=True
                if agent.assignedToInit:
                    self.CancelAgentAction(id)

                if(id==3):
                    k=0
                self.agents[id].inInit=False
                self.stations[first].outFree=False
                self.stations[first].agentComing=id
                self.agents_proto[id].reset_task()
                self.agents_proto[id].set_move_action(first,second)
                agent.finishedOrder=False
                serialized_msg = self.agents_proto[id].serialize_task()
                print(f"agent {id} is assigned to move to machine {first} - {second} to grab Product {agent.goingToProd}")
                self.message.SendMsg(serialized_msg)  

            elif agent.haveProd>0 and second=="INPUT" and not agent.startedDropping:
                if(id==3):
                    k=0
                currStation,stationPos=agent.currLocation
                if currStation != "":
                    if stationPos=="OUTPUT" and currStation != agent.init_pos[0]:
                        self.stations[currStation].outFree=True
                    elif currStation != agent.init_pos[0]:
                        self.stations[currStation].inFree=True
                if agent.assignedToInit:
                    self.CancelAgentAction(id)
                self.agents[id].inInit=False
                agent.finishedOrder=False
                self.stations[first].agentComing=id
                self.agents_proto[id].reset_task()
                self.agents_proto[id].set_move_action(first,second)
                serialized_msg = self.agents_proto[id].serialize_task()
                print(f"agent {id} is assigned to move to machine {first} - {second} to drop {agent.haveProd}")
                self.message.SendMsg(serialized_msg)  
            
        elif agent.decided and agent.finishedOrder and agent.combTo!='':
            if not agent.haveCombProd and self.stations["C-BS"].available and self.stations["C-BS"].outFree:
            #if second=="OUTPUT" and self.stations[first].outFree or second=="INPUT" and self.stations[first].inFree:
                currStation,stationPos=agent.currLocation
                if currStation != "":
                    if stationPos=="OUTPUT" and currStation != agent.init_pos[0]:
                        self.stations[currStation].outFree=True
                    elif currStation != agent.init_pos[0]:
                        self.stations[currStation].inFree=True 
                if agent.assignedToInit:
                    self.CancelAgentAction(id)
                self.agents[id].inInit=False
                self.stations["C-BS"].agentComing=id
                self.stations["C-BS"].outFree=False
                self.agents_proto[id].reset_task()
                self.agents_proto[id].set_move_action("C-BS","OUTPUT")
                agent.finishedOrder=False
                serialized_msg = self.agents_proto[id].serialize_task()
                print(f"agent {id} is assigned to move to machine C-BS - OUTPUT to grab a comb Product")
                self.message.SendMsg(serialized_msg)   

            if agent.haveCombProd and self.stations[agent.combTo].available and self.stations[agent.combTo].inFree:
                currStation,stationPos=agent.currLocation
                if currStation != "":
                    if stationPos=="OUTPUT" and currStation != agent.init_pos[0]:
                        self.stations[currStation].outFree=True
                    elif currStation != agent.init_pos[0]:
                        self.stations[currStation].inFree=True
                if agent.assignedToInit:
                    self.CancelAgentAction(id)
                self.agents[id].inInit=False
                self.stations[agent.combTo].agentComing=id
                self.stations[agent.combTo].inFree=False
                self.agents_proto[id].reset_task()
                self.agents_proto[id].set_move_action(agent.combTo,"INPUT")
                agent.finishedOrder=False
                serialized_msg = self.agents_proto[id].serialize_task()
                print(f"agent {id} is assigned to move to machine {agent.combTo} - INPUT to drop comb Product")
                self.message.SendMsg(serialized_msg)   

        elif agent.finishedOrder and agent.decided and agent.assignedToInit and not agent.inInit:
            currStation,stationPos=agent.currLocation
            if currStation != "":
                if stationPos=="OUTPUT" and currStation != agent.init_pos[0]:
                    self.stations[currStation].outFree=True
                elif currStation != agent.init_pos[0]:
                    self.stations[currStation].inFree=True
            self.agents_proto[id].reset_task()
            self.agents_proto[id].move_to_init_pos()
            print(f"agent {id} is assigned to move to its init position")
            agent.assignedToInit=True
            agent.finishedOrder=False
            serialized_msg=self.agents_proto[id].serialize_task()
            self.message.SendMsg(serialized_msg)


    def UpdateEnv(self):
        self.stations["C-BS"].Update()
        n_finished=0
        for id, prod in self.products.items():
            prod.Update()
            if prod.finished:
                n_finished+=1
        for id,agent in self.agents.items():
            self.CheckForAgent(id,agent)
            agent.Update()
            if self.agents_proto[id].redo:
                serialized_msg = self.agents_proto[id].redo_last_action()  
                if serialized_msg!="":                 
                    self.message.SendMsg(serialized_msg) 
        return n_finished==self.n_products



if __name__ == "__main__":
    
    env=Environment(config_path ='D:/111_Work/MA/connection/config3.json')
    ip = env.config["ip"]
    port = env.config["port"]
    start = time.time()
    s=41

    # handler = RobotInfoHandler()
    random.seed(s)  

    # test =b'\n\x012\x12\rCarologistics\x1a\x05robot"\x0c\x08\xc3\xb8\xb8\xb6\x06\x10\xdd\xbc\x90\x98\x022\x1d\n\x0c\x08\xc3\xb8\xb8\xb6\x06\x10\xdd\xbc\x90\x98\x02\x15\xa6\xba\xc0?\x1d\xb33\x7f@%\x00\x00\x00\x008\x02`\x01'
    # dataR3=b'\nT\n\x06Robot1\x12\rCarologistics\x1a\x05robot"\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:2\x1c\n\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:\x15\x00\x00\x80?\x1d\x00\x00\x00@%\xc3\xf5H@8\x01M\x00\x00\x00\x00P\x00`\x00'
    name=""
    field = "Field0"
    product="p19"
    version="v0"
    if env.spt:
        name += 'SPT'
    elif env.nn:
        name += 'NN'
    else:
        name += 'LPT'
    name += str(env.n_agents)
    name+='-'+field
    name+='_'+product
    name+='-'+version
    name+='-s'+str(s)
    print(name)
    # msg2= env.robot_info.process_msg(dataR3)
    # msg2= env.robot_info.process_robot_msg(test)

    # k=0
    
    with open(name, 'w') as f:
        original_stdout = sys.stdout
        sys.stdout = f

        receiver = UDPReceiver(ip, port)
        receiver.start()

        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            sock.setblocking(False)
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 5120)
            sock.bind(('', port))
            print(f"UDP socket bound to {ip}:{port} for receiving")
            env.InitMsg(sock=sock, ip=ip, port=port)

            sys.stdout.flush()

            try:
                finished=False
                while not finished:
                    env.AssignAction()

                    # Process any received messages
                    if not receiver.message_queue.empty():
                        data, addr = receiver.message_queue.get()
                        message = env.message.RcvMsg3(data, addr)
                        if message:
                            protocol_version, zero1, zero2, zero3, serializedMsg, address = message
                            if serializedMsg:
                                if address[1] == env.port and address[0] in env.agent_ips.keys():
                                    rcv_msg = env.ProcessMsg(serializedMsg,address)
                    
                    # Always call UpdateEnv, regardless of whether a message was processed
                    finished=env.UpdateEnv()

                    sys.stdout.flush()
                
                fin=time.time() - env.episodeStart
                print(f"instance finished after {time.time() - env.episodeStart}")
                fi=True

            finally:
                receiver.stop()
                receiver.join()

        sys.stdout = original_stdout


