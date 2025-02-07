import AgentTask_pb2 as Agent
import Zone_pb2 as Zone
import socket
import struct



# Define the protocol version
protocol_version = 2


ip='172.26.255.255'
#zone_message = Zone.Zone()
zone_message = Zone.C_Z18
port_send=5441
port_receive=4466
agent=Agent.AgentTask()


header1 = 8000
header2 = 502

# Create the additional message header (2x 16-bit integers, big endian)
message_header = struct.pack('!HH', header1, header2)



agent.task_id=4
#agent2=Agent.Move()
#agent.move=Agent.Move()
agent.team_color=Agent.Team.CYAN
agent.task_id = 4  # Replace 123 with the actual task ID
agent.robot_id = 1  # Replace 456 with the actual robot ID
agent.move.waypoint = "C-RS1"
agent.move.machine_point = "INPUT" #'OUTPUT'

t=1






with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)   
    sock.bind(('',port_receive))
    while True:
        print('Waiting to receive message...')
        data, address = sock.recvfrom(66596)
        if data:
            print(data)