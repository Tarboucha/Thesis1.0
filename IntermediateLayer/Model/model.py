import onnxruntime as ort
import numpy as np



class ModelOnnx:
    def __init__(self,model_path):
        self.session=ort.InferenceSession(model_path)

    def run_inference(self,input_data):
        # Get model input names
        input_names = [input.name for input in self.session.get_inputs()]
        #print(f"Expected input names: {input_names}")

        # Prepare input feed dictionary with only expected input names
        input_feed = {name: [np.array(input_data[name], dtype=np.float32)] for name in input_names}

        #print(input_feed["action_masks"])
        # Get model output name
        output_name_discrete = "discrete_actions"  # Replace with the actual name
        output_name_deterministic = "deterministic_discrete_actions"  # Replace with the actual name


        # Run inference*
        result = self.session.run([output_name_discrete, output_name_deterministic], input_feed)

        return result

    def run_inference_test(self,input_data):
        input_names = [input.name for input in self.session.get_inputs()]
        #print(f"Expected input names: {input_names}")

        # Prepare input feed dictionary with only expected input names
        input_feed = {name: np.array(input_data[name], dtype=np.float32) for name in input_names}

        print(input_feed["action_masks"])
        # Get model output name
        output_name = self.session.get_outputs()[0].name
        output_name_discrete = "discrete_actions"  # Replace with the actual name
        output_name_deterministic = "deterministic_discrete_actions"  # Replace with the actual name
        
        result = self.session.run([output_name_discrete, output_name_deterministic], input_feed)

        #result = self.session.run([output_name], input_feed)

        return result

if __name__ == "__main__":
    # Replace 'path_to_your_model.onnx' with the actual path to your ONNX model file
    model_path = "D:\\111_Work\\MA\\result\\301\\v2\\AGV\\AGV-4999012.onnx" #AGV-4999012.onnx
    session = ModelOnnx(model_path)


    # Generate random integer input data of size 166
    # Assuming the model expects a 2D input with a batch dimension, e.g., (1, 166)
    batch_size = 1
    obs_0 = np.random.randint(0, 100, size=(batch_size, 301)).astype(np.float32)

    # Generate random action masks (assuming binary masks, adjust shape if needed)
    # Shape is (batch_size, 13)
    action_masks = np.random.randint(0, 2, size=(batch_size, 13)).astype(np.float32)

    action_masks= np.array([[0]*13])
    action_masks[0][6]=1
    # Prepare input data dictionary
    input_data = {
        'obs_0': obs_0,
        'action_masks': action_masks
    }
    print("obs:")
    print(input_data["obs_0"])
    print("actions_masks:")
    print(action_masks)

    # Run inference
    result = session.run_inference_test(input_data)

    # Print the result
    print("Inference result:", result)


    session = ort.InferenceSession(model_path)

    # Get input and output names
    input_names = [input.name for input in session.get_inputs()]
    output_names = [output.name for output in session.get_outputs()]

    # Print input and output details
    for input in session.get_inputs():
        print(f"Input Name: {input.name}, Shape: {input.shape}, Type: {input.type}")

    for output in session.get_outputs():
        print(f"Output Name: {output.name}, Shape: {output.shape}, Type: {output.type}")

