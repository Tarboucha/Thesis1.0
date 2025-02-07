from . import AgentTask_pb2 as Agent
from . import Zone_pb2 as Zone
import socket
import time


class AgentTaskHandler:
    def __init__(self, robot_id, team_color='CYAN',init_waypoint='M-RS1',init_machine_point='INPUT',redo=True):
        #self.agent_task = Agent.AgentTask()
        if team_color == "CYAN":
            self.default_team_color = Agent.Team.CYAN
        else :
            self.default_team_color = Agent.Team.MAGENTA

        self.CompType={"COMP_ID":8000, "MSG_TYPE":502}
        self.default_robot_id = robot_id
        self.init_waypoint = init_waypoint
        self.init_machine_point = init_machine_point
        self.default_task_id=None #??
        self.redo=redo
        self.reset_task()
        self.lastAction={}
        self.lastAction["task_id"]=-1
        self.lastAction["waypoint"]=""
        self.lastAction["machine_point"]=""
        #self.lastAction["order_id"]=-1
        self.lastReceived={}
        self.lastReceived["task_id"]=-1
        self.lastReceived["waypoint"]=""
        self.lastReceived["machine_point"]=""
        #self.lastReceived["order_id"]=-1
        self.lastFailureStart=-1
        self.failureDuration=20
        self.currTaskId=0
        self.lastRedo=-1
        self.delay = 1

    def SetFailureStart(self,time):
        self.lastFailureStart=time

    def failureSolved(self,time):
        if time-self.lastFailureStart>self.failureDuration:
            return True
        return False

    def setLastReceived(self,msg):
        self.lastReceived["task_id"]=msg.task_id
        self.lastReceived["waypoint"]=msg.move.waypoint
        self.lastReceived["machine_point"]=msg.move.machine_point
        #self.lastReceived["robot_id"]=msg.robot_id

    def checkIfAlreadyProcessed(self,msg):
        if (self.lastReceived["task_id"]==msg.task_id and self.lastReceived["waypoint"]==msg.move.waypoint and
            self.lastReceived["machine_point"]==msg.move.machine_point ) : # and self.lastReceived["robot_id"]==msg.robot_id
            return True
        return False

    def redo_last_action(self):
        if self.lastAction["task_id"]>=0 and (time.time() - self.lastRedo)>=self.delay:
            self.lastRedo=time.time()
            self.reset_task
            self.agent_task.task_id=self.lastAction["task_id"]
            self.agent_task.move.waypoint = self.lastAction["waypoint"]
            self.agent_task.move.machine_point=self.lastAction["machine_point"]
            return self.agent_task.SerializeToString()
        return ""

    def redo_after_failure_last_action(self):
        if self.lastAction["task_id"]>=0:
            self.lastRedo=time.time()
            self.reset_task
            self.currTaskId+=1
            self.agent_task.task_id=self.currTaskId
            self.lastAction["task_id"]=self.currTaskId
            self.agent_task.move.waypoint = self.lastAction["waypoint"]
            self.agent_task.move.machine_point=self.lastAction["machine_point"]
            return self.agent_task.SerializeToString()
        return ""


    def test_cancel_action(self,task_id,waypoint,machine_point):
        self.reset_task()
        self.agent_task.task_id=task_id
        self.agent_task.move.waypoint = waypoint
        self.agent_task.move.machine_point=machine_point
        self.agent_task.cancel_task = True


    def cancel_action(self):
        self.reset_task()
        self.agent_task.task_id=self.lastAction["task_id"]
        self.agent_task.move.waypoint = self.lastAction["waypoint"]
        self.agent_task.move.machine_point=self.lastAction["machine_point"]
        self.agent_task.cancel_task = True

    def reset_task(self):
        self.agent_task = Agent.AgentTask()
        if self.default_task_id is not None:
            self.agent_task.task_id = self.default_task_id
        if self.default_robot_id is not None:
            self.agent_task.robot_id = self.default_robot_id
        self.agent_task.team_color = self.default_team_color

    def set_task_details(self, task_id=None, robot_id=None, team_color=None):
        if task_id is not None:
            self.agent_task.task_id = task_id
        if robot_id is not None:
            self.agent_task.robot_id = robot_id


    def set_move_action(self, waypoint, machine_point=None):
        self.reset_task()
        self.agent_task.move.waypoint = waypoint
        self.agent_task.task_id = self.currTaskId
        self.lastAction["waypoint"]=waypoint
        if machine_point:
            self.agent_task.move.machine_point = machine_point
            self.lastAction["machine_point"]=machine_point
        

    def move_to_init_pos(self):
        self.reset_task()
        self.agent_task.move.waypoint = self.init_waypoint
        self.agent_task.move.machine_point = self.init_machine_point
        self.agent_task.task_id = self.currTaskId
        self.lastAction["waypoint"]=self.init_waypoint
        self.lastAction["machine_point"]=self.init_machine_point

    def set_retrieve_action(self, machine_id="C-BS", machine_point="output"):
        retrieve_action = Agent.Retrieve()
        retrieve_action.machine_id = machine_id
        retrieve_action.machine_point = machine_point
        self.agent_task.retrieve.CopyFrom(retrieve_action)

    def set_deliver_action(self, machine_id, machine_point):
        deliver_action = Agent.Deliver()
        deliver_action.machine_id = machine_id
        deliver_action.machine_point = machine_point
        self.agent_task.deliver.CopyFrom(deliver_action)

    def set_buffer_station_action(self, machine_id, shelf_number):
        buffer_action = Agent.BufferStation()
        buffer_action.machine_id = machine_id
        buffer_action.shelf_number = shelf_number
        self.agent_task.buffer.CopyFrom(buffer_action)

    def set_explore_waypoint_action(self, machine_id, machine_point, waypoint):
        explore_action = Agent.ExploreWaypoint()
        explore_action.machine_id = machine_id
        explore_action.machine_point = machine_point
        explore_action.waypoint = waypoint
        self.agent_task.explore_machine.CopyFrom(explore_action)

    def serialize_task(self):
        self.currTaskId+=1
        if self.agent_task.robot_id==3:
            k=0
        if self.redo:
            self.lastAction["task_id"]=self.currTaskId
            self.lastAction["waypoint"]=self.agent_task.move.waypoint
            self.lastAction["machine_point"]=self.agent_task.move.machine_point

        self.agent_task.task_id=self.currTaskId

        return self.agent_task.SerializeToString()
    
    def process_msg(self,serialized_msg):

        message = Agent.AgentTask()
        message.ParseFromString(serialized_msg)
        return message     


def test():
    agent = AgentTaskHandler(robot_id=3)
    agent.reset_task()
    ip ='172.26.255.255'  # Replace with the target IP address
    port = 5444       # Replace with the target port


        # Create a UDP socket
    #sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    # Enable broadcasting mode
    #sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)

    #try:
    #    # Send the serialized protobuf message
    #    sock.sendto(serialized_msg, (broadcast_ip, port))
    #    print(f"Message sent to {broadcast_ip}:{port}")
    #finally:
    #    # Close the socket
    #    sock.close()

    # Create a socket and connect to the server
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)    
        sock.bind(('', port))
        print(f"UDP socket bound to {ip}:{port} for receiving")
        #sock.connect((ip, port))
        #print(f'Connected to {ip}:{port}')

        agent.move_to_init_Pos()
        # Example: Sending a message
        message = agent.serialize_task()
        send_message(sock, message)
        print(f'Sent: {message}')

        while True:
            # Wait to receive a message
            received_message = receive_message(sock)
            if received_message:
                print(f'Received: {received_message}')
                send_message(sock, message)
                print(f'Sent: {message}')
            else:
                print('Connection closed by the server.')
                break

            # Example: Sending another message

def send_message(sock, message):
    # Serialize the protobuf message
    #msg_bytes = message.SerializeToString()

    # Send the length of the message first
    #msg_length = len(msg_bytes)
    sock.sendto(message, ('172.26.255.255', 5441))
    t=1
    #sendall(msg_length.to_bytes(4, byteorder='big'))

    # Send the actual message
    #sock.sendall(msg_bytes)

def receive_message(sock):
    # Receive the length of the incoming message
    msg = sock.recv(65564)
    if not msg:
        return None


    # Parse the protobuf message
    message = Agent.Move()
    message.ParseFromString(msg)
    return message


# Example usage
if __name__ == "__main__":
    # Create an AgentTaskHandler instance with default team color (CYAN)
    agent = AgentTaskHandler(task_id=1, robot_id=3)
        
    agent.reset_task()

    # Set a move action
    #agent.set_move_action(waypoint="C_Z18")


    # Serialize the task and print it
    #serialized_task = agent.serialize_task()
    #print(serialized_task)

    test()

    # Optionally, send the task over a socket connection
    # agent.send_task(ip='127.0.0.1', port=5000)

    t=1









