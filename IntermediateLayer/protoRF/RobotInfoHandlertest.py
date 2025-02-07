from . import RobotInfo_pb2 as RobotInfo
from . import Time_pb2 as Time
from . import Pose2D_pb2 as Pose2D
from . import Team_pb2 as Team

class RobotInfoHandler:
    def __init__(self, name, team, team_color, number, host, last_seen, pose=None, vision_pose=None, state=None, maintenance_time_remaining=0.0, maintenance_cycles=0):
        #self.CompType={"COMP_ID":2000, "MSG_TYPE":31}
        self.robot_info = RobotInfo.RobotInfo()


    def process_msg(self, data):
        # Deserialize the binary string into the RobotInfo message
        message = RobotInfo.RobotInfo()
        message.ParseFromString(data)
        return message

    def get_robot_info(self):
        # Return the current RobotInfo object
        return self.robot_info

    # def print_robots(self):
    #     # Print out the details of all robots in the RobotInfo message
    #     for robot in self.robot_info.robots:
    #         print(f"Robot Name: {robot.name}")
    #         print(f"Team: {robot.team}")
    #         print(f"Team Color: {team_pb2.Team.Name(robot.team_color)}")
    #         print(f"Number: {robot.number}")
    #         print(f"Host: {robot.host}")
    #         print(f"Last Seen: {robot.last_seen.seconds} seconds, {robot.last_seen.nanos} nanos")
    #         if robot.HasField('pose'):
    #             print(f"Pose: x={robot.pose.x}, y={robot.pose.y}, theta={robot.pose.theta}")
    #         if robot.HasField('vision_pose'):
    #             print(f"Vision Pose: x={robot.vision_pose.x}, y={robot.vision_pose.y}, theta={robot.vision_pose.theta}")
    #         if robot.HasField('state'):
    #             print(f"State: {robot_info_pb2.RobotState.Name(robot.state)}")
    #         print(f"Maintenance Time Remaining: {robot.maintenance_time_remaining}")
    #         print(f"Maintenance Cycles: {robot.maintenance_cycles}")
    #         print('-' * 40)

# Example usage
if __name__ == "__main__":
    handler = RobotInfoHandler()

    # Add a robot
    handler.add_robot(
        name="3",
        team="Carologistics",
        team_color=Team.Team.CYAN,
        number=1,
        host="robot",
        last_seen={'seconds': 206065, 'nanos': 498165162},
        pose={'x': 1.0, 'y': 2.0, 'theta': 3.14},
        #state=RobotInfo.RobotState.ACTIVE
    )


    test2=b'\n\x013\x12\rCarologistics\x1a\x05robot"\x0c\x08\x8b\xe6\xb7\xb6\x06\x10\x8d\xc3\xe4\xff\x012\x1d\n\x0c\x08\x8b\xe6\xb7\xb6\x06\x10\x8d\xc3\xe4\xff\x01\x15\xc2\r{@\x1d\x04]\xb2@%\x00\x00\x00\x008\x03`\x01'
    # Serialize the robot info
    data = handler.serialize()#dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd
    dataR =b'\nU\n\x06Robot1\x12\x05TeamA\x1a\x0c192.168.1.10"\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:2\x1c\n\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:\x15\x00\x00\x80?\x1d\x00\x00\x00@%\xc3\xf5H@8\x01@\x01M\x00\x00\x00\x00P\x00`\x00'
    dataR2 =b'\nS\n\x03jaw\x12\rCarologistics\x1a\x05robot"\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:2\x1c\n\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:\x15\x00\x00\x80?\x1d\x00\x00\x00@%\xc3\xf5H@8\x01@\x01M\x00\x00\x00\x00P\x00`\x00'
    dataR3=b'\nT\n\x06Robot1\x12\rCarologistics\x1a\x05robot"\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:2\x1c\n\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:\x15\x00\x00\x80?\x1d\x00\x00\x00@%\xc3\xf5H@8\x01M\x00\x00\x00\x00P\x00`\x00'
    print(data)#dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd
    
    dataR4=b'\nF\n\x06Robot1\x12\rCarologistics\x1a\x05robot"\x04\x08\x02\x10\x022\x15\n\x04\x08\x02\x10\x02\x15\x00\x00\x80?\x1d\x00\x00\x00@%\xc3\xf5H@8\x01M\x00\x00\x00\x00P\x00`\x00'
    msg = handler.process_msg(data)
    
    test=b'\n\x013\x12\rCarologistics\x1a\x05robot"\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:2\x1c\n\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:\x15\x00\x00\x80?\x1d\x00\x00\x00@%\xc3\xf5H@8\x01@\x01M\x00\x00\x00\x00P\x00`\x00'
    data3=b'\nc\n\x0c192.168.1.10\x12\rCarologistics\x1a\x0c192.168.1.10"\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:2\x1c\n\x0b\x08\x90\x8c\x9a\xa7\x06\x10\xc0\xa9\xd3:\x15\x00\x00\x80?\x1d\x00\x00\x00@%\xc3\xf5H@8\x01@\x01M\x00\x00\x00\x00P\x00`\x00'
    data2=b'\n\x013\x12\rCarologistics\x1a\x05robot"\x0c\x08\xab\xc7\xb7\xb6\x06\x10\xe0\x8e\xd1\xef\x012\x1d\n\x0c\x08\xab\xc7\xb7\xb6\x06\x10\xe0\x8e\xd1\xef\x01\x15\xc2\r{@\x1d\x04]\xb2@%\x00\x00\x00\x008\x03`\x01'
    msg2=handler.process_msg(test2)
    k=0
    # # Create a new handler and deserialize the data
    # new_handler = RobotInfoHandler()
    # new_handler.deserialize(data)

    # # Print robot info from the new handler
    # new_handler.print_robots()