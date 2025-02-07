import struct
import select
import socket
import errno

class Msg:
    def __init__(self,sock,header1=8000,header2=502,receive_ip={1:'172.26.108.81',2:'172.26.108.82',3:'172.26.108.83'},broadcast_ip='172.26.255.255',port=5441):
        self.protocol_version=2
        self.header1=header1
        self.header2=header2
        self.message_header=struct.pack('!HH',header1,header2)
        self.sock=sock
        self.broadcast_ip=broadcast_ip
        self.receive_ip=receive_ip
        self.port=port
        self.verbose=True

    def SendMsg(self,msg):
        protobuf_size = len(msg)+4
        # Create the header
        # Protocol version (1 byte), three 8-bit zero values (3 bytes), protobuf size (4 bytes, big endian)
        header = struct.pack('!BBBBI', self.protocol_version, 0, 0, 0, protobuf_size)
        message_with_header = header + self.message_header + msg
        self.last_msg=message_with_header
        self.sock.sendto(message_with_header, (self.broadcast_ip, self.port)) 
        if self.verbose:
            print("message successfully sent")

    def RcvMsg(self):
        data, address = self.sock.recvfrom(66596)
        return data,address
    
    def RcvMsg1(self):
        data, address = self.sock.recvfrom(66596)
        if(address==(self.ip,self.port)):
            protocol_version, zero1, zero2, zero3, protobuf_size = struct.unpack('!BBBBI', data[:8])
            serialized_msg = data[8+4:8+4 + (protobuf_size - 4)]  # Subtract 4 because protobuf_size includes header size

        return protocol_version, zero1, zero2, zero3, serialized_msg


    def RcvMsg3(self,data,address):

        if address[1] ==  self.port and address[0] in self.receive_ip.values() :
            protocol_version, zero1, zero2, zero3, protobuf_size = struct.unpack('!BBBBI', data[:8])
            serialized_msg = data[12:12 + (protobuf_size - 4)]  # Adjust for protobuf size
            return protocol_version, zero1, zero2, zero3, serialized_msg, address
        
        return None


    def RcvMsg2(self):
        # Using select to wait for the socket to be ready for reading
        ready_socks, _, _ = select.select([self.sock], [], [], 0.001)  # 0.1 seconds timeout

        if self.sock in ready_socks:
            data, address = self.sock.recvfrom(66596)
            #print(address[0])
            if address[1] ==  self.port and address[0] in self.receive_ip.values() :
                protocol_version, zero1, zero2, zero3, protobuf_size = struct.unpack('!BBBBI', data[:8])
                serialized_msg = data[12:12 + (protobuf_size - 4)]  # Adjust for protobuf size
                return protocol_version, zero1, zero2, zero3, serialized_msg, address

        # If no data was received, return None or perform other operations
        return None


        # while True:
    #     print('Waiting to receive message...')
    #     data, address = sock.recvfrom(66596)
    #     if data:
    #         print(data)

    #         print(f'Received {len(data)} bytes from {address}')
    #         if(address==('172.26.108.81',5441)):
    #             # Extract the header
    #             protocol_version, zero1, zero2, zero3, protobuf_size = struct.unpack('!BBBBI', data[:8])
    #             print(f'Header - Protocol version: {protocol_version}, Size: {protobuf_size}')

    #             # Extract the Protobuf message
    #             serialized_msg = data[8+4:8+4 + (protobuf_size - 4)]  # Subtract 4 because protobuf_size includes header size

    #             # Deserialize the Protobuf message
    #             message = Agent.AgentTask()
    #             message.ParseFromString(serialized_msg)

    #             print('Received message:')
    #             print(message)
