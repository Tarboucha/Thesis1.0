import json


def proceed_file(input_file):
    with open(input_file, 'r') as f: 
        json_objects = []
        for line in f:
            line = line.strip()
            if line:  # Skip empty lines
                try:
                    json_object = json.loads(line)
                    json_objects.append(json_object)
                except json.JSONDecodeError as e:
                    print(f"Error decoding JSON: {e}. Skipping line: {line}")
    select_attributes(json_objects)

def select_attributes(json_objects):
    # Read input JSON file
    #print(data[0])
    #print(type(data['stations']))
    n=0
    for item in json_objects:
        stations = item['machines']
        products = item['orders']
        charact = item['ring_specs']
        station_str='station'+str(n)
        product_str='product'+str(n)
        charact_str='charact'+str(n)
        output_charact='D:/111_Work/Instances/new/characts/'+charact_str
        output_station='D:/111_Work/Instances/new/stations/'+station_str
        output_product='D:/111_Work/Instances/new/products/'+product_str
        with open(output_station, 'w') as fi:
            json.dump(stations, fi, indent=4)
        with open(output_charact, 'w') as fi:
            json.dump(charact, fi, indent=4)
        with open(output_product, 'w') as fi:
            json.dump(products, fi, indent=4)
        n+=1


# Example usage
#input_file = 'D:/111_Work/Instances/game_report3.txt'  # Replace with your input file name
input_file = 'D:/111_Work/Instances/fh_field_size'
output_filename = 'fh_test.json'  # Replace with your output file name


proceed_file(input_file)
