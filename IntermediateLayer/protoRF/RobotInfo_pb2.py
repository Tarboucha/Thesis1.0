# -*- coding: utf-8 -*-
# Generated by the protocol buffer compiler.  DO NOT EDIT!
# source: RobotInfo.proto
# Protobuf Python Version: 4.25.0
"""Generated protocol buffer code."""
from google.protobuf import descriptor as _descriptor
from google.protobuf import descriptor_pool as _descriptor_pool
from google.protobuf import symbol_database as _symbol_database
from google.protobuf.internal import builder as _builder
# @@protoc_insertion_point(imports)

_sym_db = _symbol_database.Default()


from . import Time_pb2 as Time__pb2
from . import Pose2D_pb2 as Pose2D__pb2
from . import Team_pb2 as Team__pb2


DESCRIPTOR = _descriptor_pool.Default().AddSerializedFile(b'\n\x0fRobotInfo.proto\x12\tllsf_msgs\x1a\nTime.proto\x1a\x0cPose2D.proto\x1a\nTeam.proto\"\xe4\x02\n\x05Robot\x12\x0c\n\x04name\x18\x01 \x02(\t\x12\x0c\n\x04team\x18\x02 \x02(\t\x12#\n\nteam_color\x18\x0c \x02(\x0e\x32\x0f.llsf_msgs.Team\x12\x0e\n\x06number\x18\x07 \x02(\r\x12\x0c\n\x04host\x18\x03 \x02(\t\x12\"\n\tlast_seen\x18\x04 \x02(\x0b\x32\x0f.llsf_msgs.Time\x12\x1f\n\x04pose\x18\x06 \x01(\x0b\x32\x11.llsf_msgs.Pose2D\x12&\n\x0bvision_pose\x18\x0b \x01(\x0b\x32\x11.llsf_msgs.Pose2D\x12$\n\x05state\x18\x08 \x01(\x0e\x32\x15.llsf_msgs.RobotState\x12%\n\x1amaintenance_time_remaining\x18\t \x01(\x02:\x01\x30\x12\x1a\n\x12maintenance_cycles\x18\n \x01(\r\"&\n\x08\x43ompType\x12\x0c\n\x07\x43OMP_ID\x10\xd0\x0f\x12\x0c\n\x08MSG_TYPE\x10\x1f\"U\n\tRobotInfo\x12 \n\x06robots\x18\x01 \x03(\x0b\x32\x10.llsf_msgs.Robot\"&\n\x08\x43ompType\x12\x0c\n\x07\x43OMP_ID\x10\xd0\x0f\x12\x0c\n\x08MSG_TYPE\x10\x1e*;\n\nRobotState\x12\n\n\x06\x41\x43TIVE\x10\x01\x12\x0f\n\x0bMAINTENANCE\x10\x02\x12\x10\n\x0c\x44ISQUALIFIED\x10\x03\x42\x32\n\x1forg.robocup_logistics.llsf_msgsB\x0fRobotInfoProtos')

_globals = globals()
_builder.BuildMessageAndEnumDescriptors(DESCRIPTOR, _globals)
_builder.BuildTopDescriptorsAndMessages(DESCRIPTOR, 'RobotInfo_pb2', _globals)
if _descriptor._USE_C_DESCRIPTORS == False:
  _globals['DESCRIPTOR']._options = None
  _globals['DESCRIPTOR']._serialized_options = b'\n\037org.robocup_logistics.llsf_msgsB\017RobotInfoProtos'
  _globals['_ROBOTSTATE']._serialized_start=514
  _globals['_ROBOTSTATE']._serialized_end=573
  _globals['_ROBOT']._serialized_start=69
  _globals['_ROBOT']._serialized_end=425
  _globals['_ROBOT_COMPTYPE']._serialized_start=387
  _globals['_ROBOT_COMPTYPE']._serialized_end=425
  _globals['_ROBOTINFO']._serialized_start=427
  _globals['_ROBOTINFO']._serialized_end=512
  _globals['_ROBOTINFO_COMPTYPE']._serialized_start=474
  _globals['_ROBOTINFO_COMPTYPE']._serialized_end=512
# @@protoc_insertion_point(module_scope)
