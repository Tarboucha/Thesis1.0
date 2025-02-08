import json

import matplotlib.pyplot as plt

# Assuming 'formated_product' is your list of tuples
# Example: formated_product = [(1, 2), (3, 2), (1, 4), (5, 1), (3, 3)]




def seconds_to_minutes_seconds(seconds):
    minutes = seconds // 60 
    remaining_seconds = (seconds % 60)/100.0
    return minutes + remaining_seconds

def separate(data_stations):
    C_stations=[]
    M_stations=[]
    for item in data_stations:
        temp_station={}
        if item['team']=='CYAN':
            temp_station['name'] = item['name']
            temp_station['mtype'] = item['mtype']
            temp_station['rotation'] = item['rotation']
            temp_station['zone'] = item['zone']
            if item['mtype']=='RS':
                temp_station['available_colors']=item['available_colors']
            C_stations.append(temp_station)
        else:
            temp_station['name'] = item['name']
            temp_station['mtype'] = item['mtype']
            temp_station['rotation'] = item['rotation']
            temp_station['zone'] = item['zone']
            if item['mtype']=='RS':
                temp_station['available_colors']=item['available_colors']
            M_stations.append(temp_station)
    return C_stations, M_stations, 

def assignSpec2(data_stations,data_characts):
    dict_bases={}
    dict_r1tasks={}
    dict_r2tasks={}
    dict_color_n={}
    for item in data_stations:
        temp=item['name'][2:]
        if item['name'][2:]=='RS1':
            dict_r1tasks[item['name']]=item['available_colors']
            n=2
            for it in item['available_colors']:
                dict_color_n[it]=n
                n+=1

        elif item['name'][2:]=='RS2':
            dict_r2tasks[item['name']]=item['available_colors']
            n=4
            for it in item['available_colors']:
                dict_color_n[it]=n
                n+=1

    for item in data_characts:
        dict_bases[dict_color_n[item['color']]]=item['req_bases']

    return dict_bases, dict_color_n, dict_r1tasks, dict_r2tasks

def assignSpec(data_stations,data_characts):
    dict_bases={}
    dict_rstations={}
    temp=2
    for item in data_characts:
        if item['req_bases']==1:
            dict_bases[item['color']]=4
        elif item['req_bases']==2:
            dict_bases[item['color']]=5
        elif item['req_bases']==0:
            dict_bases[item['color']]=temp
            temp+=1

    for item in data_stations:
        if item['mtype']=='RS':
            dict_rstations[item['name']]=item['available_colors']
    
    return dict_bases, dict_rstations

def zone_to_loc(zone):
    loc=[0.5,-3.5]
    if zone[0]=='M':
        loc[0]-=int(zone[3])
        loc[1]+=int(zone[4])-1
    if zone[0]=='C':
        loc[0]+=int(zone[3])-1
        loc[1]+=int(zone[4])-1
    return loc



def format_stations(dict_stations,dict_colorNums,dict_cstations,dict_bases):
    formated_stations=[]
    
    for item in dict_stations:
        temp={}
        if item['mtype']=='BS':
            temp['type']='BaseStation'
        elif item['mtype']=='SS':
            continue
        elif item['mtype']=='RS':
            temp['type']='RingStation'
        elif item['mtype']=='DS':
            temp['type']='DeliveryStation'
        elif item['mtype']=='CS':
            temp['type']='CapStation'

        loc=zone_to_loc(item['zone'])
        temp['position']={'x':loc[0],'y':0,'z':loc[1]}
        temp['rotation']={'x':0, 'y':item['rotation'],'z':0}
        temp['scale']={'x':1, 'y':1,'z':1}
        
        if temp['type']=='RingStation':
            temp2=[]
            for it in item['available_colors']:
                temp3={"taskNumber":[dict_colorNums[it], dict_bases[dict_colorNums[it]]]}
                temp2.append(temp3)

            temp["ringtasks"]=temp2
        elif item['mtype']=='CS':
            if item['name'][4]=='2':
                temp['capTask']=dict_cstations['CAP_BLACK']
            else:
                temp['capTask']=dict_cstations['CAP_GREY']    

        formated_stations.append(temp)
    return {'stations':formated_stations}

def format_product(dict_product,dict_colorNums,dict_cstations):
    formated_products=[]
    n_rings=0
    for item in dict_product:
        temp={}
        temp['type']='Product'
        temp["startingTime"]=seconds_to_minutes_seconds(item['start_range'][0])
        temp2=[]
        for it in item['ring_colors']:
            temp3={"taskNumber":dict_colorNums[it]}
            temp2.append(temp3)
            n_rings+=1
        temp["ringElements"]=temp2
        temp['capElement']=dict_cstations[item['cap_color']]
        formated_products.append(temp)
    
    return {"products": formated_products},len(formated_products),n_rings


if __name__ == '__main__':
    n=0

    cproduct_base_path = 'D:/111_Work/Instances/product/cproduct/product'
    mproduct_base_path = 'D:/111_Work/Instances/product/mproduct/product'
    cstation_base_path = 'D:/111_Work/Instances/station/cstation/cstation'
    mstation_base_path = 'D:/111_Work/Instances/station/mstation/mstation'

    cproduct_path_dict = {i: [f"{cproduct_base_path}{i}/",0] for i in range(1, 21)}
    mproduct_path_dict = {i: [f"{mproduct_base_path}{i}/",0] for i in range(1, 21)}
    cstation_path_dict = {i: [f"{cstation_base_path}{i}/",0] for i in range(1, 21)}
    mstation_path_dict = {i: [f"{mstation_base_path}{i}/",0] for i in range(1, 21)}

    products_characts=[]
    while(n<9999):

        station_str='station'+str(n)
        product_str='product'+str(n)
        charact_str='charact'+str(n)

        characts_path='D:/111_Work/Instances/new/characts/'+charact_str
        stations_path='D:/111_Work/Instances/new/stations/'+station_str
        products_path='D:/111_Work/Instances/new/products/'+product_str

        # Cstations_out_path='D:/111_Work/Instances/extracted/Cstation3/C'+station_str+'.json'
        # Mstations_out_path='D:/111_Work/Instances/extracted/Mstation3/M'+station_str+'.json'
        products_cyan_out_path='D:/111_Work/Instances/extracted/product4/Cyan/'+product_str+'.json'
        products_magenta_out_path='D:/111_Work/Instances/extracted/product4/Magenta/'+product_str+'.json'

        charact_out_path=''


        with open(characts_path, 'r') as f:
            data_characts = json.load(f)
        with open(stations_path, 'r') as f:
            data_stations = json.load(f)
        with open(products_path, 'r') as f:
            data_product = json.load(f)

        dict_cstation={'CAP_GREY':6,'CAP_BLACK':7}

        C_stations, M_stations = separate(data_stations)
        dict_bases,c_color_n, C_dict_r1tasks, C_dict_r2tasks = assignSpec2(C_stations,data_characts)
        dict_bases,m_color_n, M_dict_r1tasks, M_dict_r2tasks = assignSpec2(M_stations,data_characts)

        formated_C_station=format_stations(C_stations,c_color_n,dict_cstation,dict_bases=dict_bases)   
        formated_M_station=format_stations(M_stations,m_color_n,dict_cstation,dict_bases=dict_bases)

        c_formated_Products,n_products,n_rings=format_product(dict_product=data_product,dict_colorNums=c_color_n,dict_cstations=dict_cstation)
        m_formated_Products,n_products,n_rings=format_product(dict_product=data_product,dict_colorNums=m_color_n,dict_cstations=dict_cstation)
        products_characts.append((n_products,n_rings))


        product_n=cproduct_path_dict[n_rings][1]
        cproduct_path_dict[n_rings][1]=cproduct_path_dict[n_rings][1]+1
        cproducts_out_path=cproduct_path_dict[n_rings][0]+'product'+str(product_n)+'.json'
        mproducts_out_path=mproduct_path_dict[n_rings][0]+'product'+str(product_n)+'.json'
        Cstations_out_path = cstation_path_dict[n_rings][0]+'cstation'+str(product_n)+'.json'
        Mstations_out_path = mstation_path_dict[n_rings][0]+'mstation'+str(product_n)+'.json'

        with open(Cstations_out_path, 'w') as fi:
            json.dump(formated_C_station, fi, indent=4)
        with open(Mstations_out_path, 'w') as fi:
            json.dump(formated_M_station, fi, indent=4)
        with open(cproducts_out_path, 'w') as fi:
            json.dump(c_formated_Products, fi, indent=4)
        with open(mproducts_out_path, 'w') as fi:
            json.dump(m_formated_Products, fi, indent=4)
        n+=1


    # Extract the first and second values from the tuples
    first_values = [item[0] for item in products_characts]
    second_values = [item[1] for item in products_characts]
# Create histograms for the first and second values
    plt.figure(figsize=(14, 6))
    # Plot for the first values
    plt.subplot(1, 2, 1)
    plt.hist(first_values, bins=range(min(first_values), max(first_values) + 1), edgecolor='black', alpha=0.7)
    plt.title('Histogram of First Values')
    plt.xlabel('First Value')
    plt.ylabel('Frequency')

    # Plot for the second values
    plt.subplot(1, 2, 2)
    plt.hist(second_values, bins=range(min(second_values), max(second_values) + 1), edgecolor='black', alpha=0.7)
    plt.title('Histogram of Second Values')
    plt.xlabel('Second Value')
    plt.ylabel('Frequency')

    # Display the histograms
    plt.tight_layout()
    plt.show()















