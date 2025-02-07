import AgentTask_pb2 as Agent
import Zone_pb2 as Zone
import socket
import struct


class Msg:
    def __init__(self,sock,header1=8000,header2=502,ip='172.26.255.255',port=5441):
        self.protocol_version=2
        self.header1=header1
        self.header2=header2
        self.message_header=struct.pack('!HH',header1,header2)
        self.sock=sock
        self.ip=ip
        self.port=port

    def SendMsg(self,msg,verbose=False):
        protobuf_size = len(msg)+4
        # Create the header
        # Protocol version (1 byte), three 8-bit zero values (3 bytes), protobuf size (4 bytes, big endian)
        header = struct.pack('!BBBBI', protocol_version, 0, 0, 0, protobuf_size)
        message_with_header = header + self.message_header + msg
        self.last_msg=message_with_header
        sock.sendto(message_with_header, (self.ip, self.port))
        if verbose:
            print("message successfully sent")

    def RcvMsg(self):
        data, address = self.sock.recvfrom(66596)
        return data, address
    


if __name__ == "__main__":
    # Define the protocol version
    protocol_version = 2


    ip='172.26.255.255'
    #zone_message = Zone.Zone()
    zone_message = Zone.C_Z18
    port_send=5441
    port_receive=5441
    agent=Agent.AgentTask()


    header1 = 8000
    header2 = 502

    # Create the additional message header (2x 16-bit integers, big endian)
    message_header = struct.pack('!HH', header1, header2)



    #agent.task_id=4
    #agent2=Agent.Move()
    #agent.move=Agent.Move()
    agent.team_color=Agent.Team.CYAN
    agent.task_id = 4  # Replace 123 with the actual task ID
    agent.robot_id = 3 # Replace 456 with the actual robot ID
    #agent.cancel_task = True
    agent.move.waypoint = "C-RS1"
    agent.move.machine_point = "OUTPUT" #'OUTPUT'


    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)    
        sock.bind(('', port_send))
        print(f"UDP socket bound to {ip}:{port_send} for receiving")
        #sock.connect((ip, port)
        # Example: Sending a message
        serialized_msg = agent.SerializeToString()
        

        # Calculate the size of the serialized protobuf message
        protobuf_size = len(serialized_msg)+4
        print(protobuf_size)
        # Create the header
        # Protocol version (1 byte), three 8-bit zero values (3 bytes), protobuf size (4 bytes, big endian)
        header = struct.pack('!BBBBI', protocol_version, 0, 0, 0, protobuf_size)

        # Combine the header and the serialized message
        message_with_header = header + message_header + serialized_msg

        print(f'Sent: {serialized_msg}')
        sock.sendto(message_with_header, ('172.26.255.255', 5441))

        while True:
            print('Waiting to receive message...')
            data, address = sock.recvfrom(66596)
            if data:
                print(data)

                print(f'Received {len(data)} bytes from {address}')
                if(address==('172.26.108.81',5441)):
                    # Extract the header
                    protocol_version, zero1, zero2, zero3, protobuf_size = struct.unpack('!BBBBI', data[:8])
                    print(f'Header - Protocol version: {protocol_version}, Size: {protobuf_size}')

                    # Extract the Protobuf message
                    serialized_msg = data[8+4:8+4 + (protobuf_size - 4)]  # Subtract 4 because protobuf_size includes header size

                    # Deserialize the Protobuf message
                    message = Agent.AgentTask()
                    message.ParseFromString(serialized_msg)

                    print('Received message:')
                    print(message)

                    if( message.successful==True):
                        print("action was successful")

                    break