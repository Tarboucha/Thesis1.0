using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class FASController : MonoBehaviour
{
    private FASinfo info;

    // Lists of all the elements within FAS
    private List<Transform> childs;
    public List<Product> products;
    public List<TemporalStation> tables;
    public List<Workstation> workstations;
    public List<BaseStation> basestations;
    public List<EmptyStation> emptystations;
    // element 4 requires 1 base element and 6 2
    private Dictionary<int, int> ringstationsTasks;
    private Dictionary<int, int> capstationsTasks;
    private Dictionary<float, Vector3> stationsPosition;
    public Dictionary<int, BaseStation> baseStationDictionary;
    public int bStationInstID = 0;
    public int dStationInstID = 0;
    //1 starting time 2 entry number of ring elements 3 ring element tasks 4 capstation 
    private List<float[]> productsCharacteristics;
    private Dictionary<int, int> combProdRequired;


    //private int n_products = 9;
    public GameObject BaseStation;
    public GameObject RingStation;
    public GameObject CapStation;
    public GameObject DeliveryStation;
    public GameObject productObject;
    public GameObject EmptyStation;
    public int instance_seed = 1; //should be prime

    public NavMeshSurface navSurface;
    public int numIntansces = 2450;
    public int instanceIncr = 1;  /// 6
    private bool randomness = false;
    /// </summary>


    //private StatsRecorder statsRecorder;


    [System.Serializable]
    public class ObjectProduct
    {
        public string type="Product";
        public float startingTime=1.1F;
        public List<ringTask> ringElements=null;
        public int capElement=-1;
    }

    [System.Serializable]
    public class ProductList
    {
        public ObjectProduct[] products;
    }

    [System.Serializable]
    public class StationList
    {
        public ObjectStation[] stations;
    }

    public StationList myStations = new StationList();
    public ProductList myProducts = new ProductList();
    
    [System.Serializable]
    public class ObjectStation
    {
        public string type;
        public Position position;
        public Rotation rotation;
        public Scale scale;
        public List<ringTaskS> ringtasks;
        public int capTask;
    }

    [System.Serializable]
    public class Position
    {
        public float x;
        public float y;
        public float z;
    }

    [System.Serializable]
    public class Rotation
    {
        public float x;
        public float y;
        public float z;
    }

    [System.Serializable]
    public class Scale
    {
        public float x;
        public float y;
        public float z;
    }

    [System.Serializable]
    public class ringTask
    {
        public int taskNumber;
    }

    [System.Serializable]
    public class ringTaskS
    {
        public List<int> taskNumber;
    }



    void Awake()
    {
        //initialize variables
        childs = new List<Transform>();
        products = new List<Product>();
        tables = new List<TemporalStation>();
        workstations = new List<Workstation>();
        basestations = new List<BaseStation>();
        ringstationsTasks = new Dictionary<int, int>();
        stationsPosition = new Dictionary<float, Vector3>();
        capstationsTasks = new Dictionary<int, int>();
        productsCharacteristics = new List<float[]>();
        baseStationDictionary = new Dictionary<int, BaseStation>();
        combProdRequired = new Dictionary<int, int>();
        //statsRecorder = Academy.Instance.StatsRecorder;
        info = transform.GetComponent<FASinfo>();
        randomness = info.randomness;
        instanceIncr = info.startingInst;
        NextInst();


        //UnityEngine.Random.InitState(info.jobSeed); //121
        //ObjectData[] objects = JsonUtility.FromJson<ObjectData[]>(jsonString);
        Rebake();

        
    }
    /*
    public void start()
    {
        CollectChilds();
        for (int i = 0; i < n_products; i++)
        {
            int temp = UnityEngine.Random.Range(1, basestations.Count);
            Vector3 outputLoc;
            outputLoc = basestations[temp].GetOutputLocation().Item1;
            Instantiate(productObject, outputLoc, Quaternion.identity);
        }
    }
    */
    public void Rebake()
    {
        navSurface.BuildNavMesh();
    }

    public void NextInst()
    {
        InstStations();
        InstEmptyStations();
        InstProducts();
        if (randomness)
        {
            instanceIncr = UnityEngine.Random.Range(0, 9999);
            int s = UnityEngine.Random.Range(1, 999999);
            info.setSeed(s);
            UnityEngine.Random.InitState(s);
        }
        else
        {
            instanceIncr++;
            if (info.seedIncr)
            {
                info.incrSeed();
            }
            if (info.jobSeed > 0)
            {
                UnityEngine.Random.InitState(info.jobSeed);
            }
        }

    }
    //"D:\\111_Work\\Instances\\simToReal\\Field1M"
    //"/home/Instances/extracted/Mstation"
    //"D:\\111_Work\\Instances\\extracted\\Mstation" M
    //"/home/Instances/station/mstation/mstation15"
    //"D:\\111_Work\\Instances\\station\\mstation\\mstation15"
    public void InstEmptyStations()
    {
        int currInstNum = (instanceIncr * instance_seed) % numIntansces;
        string pathStation = Path.Combine("D:\\111_Work\\Instances\\extracted\\Cstation", "Cstation" + $"{currInstNum}" + ".json");  //"/home/Instances/extracted/Mstation" "D:\\111_Work\\Instances\\extracted\\Mstation"
        //string pathStation = Path.Combine("D:\\111_Work\\Instances\\simToReal\\Field5M" + ".json");  //"/home/Instances/extracted/Mstation" "D:\\111_Work\\Instances\\extracted\\Mstation"

        if (File.Exists(pathStation))
        {
            Debug.Log("File for instance empty Stations " + currInstNum + " correctly loaded");
            //statsRecorder.Add("Intansce/Cstations", instanceIncr);
            string fileContents = File.ReadAllText(pathStation);
            StationList myStations = JsonUtility.FromJson<StationList>(fileContents);
            CollectEmptyStations(myStations);

        }
        else
        {
            Time.timeScale = 0f; // Set time scale to 0 to pause the game
            Debug.Log("Game quitted since Characteristic file for station not found");
            Application.Quit();
        }
    }
    //"D:\\111_Work\\Instances\\simToReal\\Field1C"
    //"/home/Instances/extracted/Cstation"
    //"D:\\111_Work\\Instances\\extracted\\Cstation" C
    //"/home/Instances/station/cstation/cstation15"
    // "D:\\111_Work\\Instances\\station\\cstation\\cstation15"
    public void InstStations() {
        int currInstNum = (instanceIncr*instance_seed) % numIntansces;
        string pathStation = Path.Combine("D:\\111_Work\\Instances\\extracted\\Mstation", "Mstation" + $"{currInstNum}" + ".json");  //"/home/Instances/extracted/Cstation" "D:\\111_Work\\Instances\\extracted\\Cstation"
        //string pathStation = Path.Combine("D:\\111_Work\\Instances\\simToReal\\Field5C" + ".json");

        if (File.Exists(pathStation))
        {
            Debug.Log("File for instance Stations " + currInstNum + " correctly loaded");
            //statsRecorder.Add("Intansce/Cstations", instanceIncr);
            string fileContents = File.ReadAllText(pathStation);
            StationList myStations = JsonUtility.FromJson<StationList>(fileContents);
            CollectStations(myStations);

        }
        else
        {
            Time.timeScale = 0f; // Set time scale to 0 to pause the game
            Debug.Log("Game quitted since Characteristic file for station not found");
            Application.Quit();
        }
    }

    //"/home/Instances/extracted/product"
    //"D:\\111_Work\\Instances\\extracted\\product4\\Cyan" 
    //"D:\\111_Work\\Instances\\product\\cproduct\\product15" 2450
    //"/home/Instances/product/cproduct/product15"
    public void InstProducts()
    {
        int currInstNum = 2;//2; (instanceIncr * instance_seed) % numIntansces;// 2; //Path.Combine("D:\\111_Work\\Instances\\extracted\\product","product" + $"{currInstNum}" + ".json"); //D:\111_Work\Instances\product\product15 
        string pathProduct = Path.Combine("D:\\111_Work\\Instances\\extracted\\product4\\Cyan", "product" + $"{currInstNum}" + ".json"); //"D:\\111_Work\\Instances\\extracted\\product" "/home/Instances/extracted/product"
        //string pathProduct = Path.Combine("D:\\111_Work\\Instances\\simToReal\\product11" + ".json");
        if (File.Exists(pathProduct))
        {
            Debug.Log("File for instance Products " + currInstNum + " correctly loaded");
            //statsRecorder.Add("Intansce/Product", instanceIncr);
            string fileContents = File.ReadAllText(pathProduct);
            ProductList myProducts = JsonUtility.FromJson<ProductList>(fileContents);
            CollectProducts(myProducts);
        }
        else
        {
            Time.timeScale = 0f; // Set time scale to 0 to pause the game
            Debug.Log("Game quitted since Characteristic file for product not found");
            Application.Quit();
        }
    }

    public void EpisodeReset()
    {
        childs = new List<Transform>();
        products = new List<Product>();
        tables = new List<TemporalStation>();
        workstations = new List<Workstation>();
        basestations = new List<BaseStation>();
        ringstationsTasks = new Dictionary<int, int>();
        stationsPosition = new Dictionary<float, Vector3>();
        capstationsTasks = new Dictionary<int, int>();
        productsCharacteristics = new List<float[]>();
        baseStationDictionary = new Dictionary<int, BaseStation>();
        combProdRequired = new Dictionary<int, int>(); 
    }

    /*
    public void TempReset()
    {
        //find next station and product characteristics
        pathStation = Path.Combine("D:\\111_Work\\unity\\station", "test.json");
        pathProduct = Path.Combine("D:\\111_Work\\unity\\product", "testProd.json");

        if (File.Exists(pathStation) && File.Exists(pathProduct))
        {
            string fileContents = File.ReadAllText(pathStation);
            StationList myStations = JsonUtility.FromJson<StationList>(fileContents);
            CollectStations(myStations);

            fileContents = File.ReadAllText(pathProduct);
            ProductList myProducts = JsonUtility.FromJson<ProductList>(fileContents);
            CollectProducts(myProducts);
        }
        else
        {
            Time.timeScale = 0f; // Set time scale to 0 to pause the game
            Debug.Log("Game paused since Characteristic files not found");
        }
    }
    */
    public void CollectProducts(ProductList myProducts)
    {
        if (myProducts.products.Length > 0)
        {
            foreach (var obj in myProducts.products)
            {
                if (obj.type == "Product")
                {
                    float[] currProd = new float[3 + obj.ringElements.Count];
                    currProd[0] = obj.startingTime;
                    currProd[1] = obj.ringElements.Count;
                    for(int i= 0; i < obj.ringElements.Count; i++)
                    {
                        currProd[2 + i] = obj.ringElements[i].taskNumber;
                    }
                    currProd[2 + obj.ringElements.Count] = obj.capElement;

                    productsCharacteristics.Add(currProd);
                    //Vector3 processLoc = baseStationDictionary[bStationInstID].GetProcessLocation();
                    //Instantiate(productObject, processLoc, Quaternion.identity, baseStationDictionary[bStationInstID].transform);
                }
            }
        }
    }

    public void CollectEmptyStations(StationList myStations)
    {
        if (myStations.stations.Length > 0)
        {
            foreach (var obj in myStations.stations)
            {
                if (obj.type == "BaseStation")
                {
                    Vector3 processLoc = new Vector3(obj.position.x, 0.1f, obj.position.z);
                    processLoc += transform.position;
                    Quaternion rotation = Quaternion.Euler(new Vector3(obj.rotation.x, obj.rotation.y, obj.rotation.z));

                    GameObject e_stationObject = Instantiate(EmptyStation, processLoc, rotation, transform);
                    //b_stationObject.transform.parent = transform;
                    EmptyStation e_station = e_stationObject.GetComponent<EmptyStation>();
                    emptystations.Add(e_station);
                }
                else if (obj.type == "DeliveryStation")
                {
                    Vector3 processLoc = new Vector3(obj.position.x, 0.1f, obj.position.z);
                    processLoc += transform.position;
                    Quaternion rotation = Quaternion.Euler(new Vector3(obj.rotation.x, obj.rotation.y, obj.rotation.z));

                    GameObject e_stationObject = Instantiate(EmptyStation, processLoc, rotation, transform);
                    //b_stationObject.transform.parent = transform;
                    EmptyStation e_station = e_stationObject.GetComponent<EmptyStation>();
                    emptystations.Add(e_station);
                }
                else if (obj.type == "RingStation")
                {
                    Vector3 processLoc = new Vector3(obj.position.x, 0.1f, obj.position.z);
                    processLoc += transform.position;
                    Quaternion rotation = Quaternion.Euler(new Vector3(obj.rotation.x, obj.rotation.y, obj.rotation.z));

                    GameObject e_stationObject = Instantiate(EmptyStation, processLoc, rotation, transform);
                    //b_stationObject.transform.parent = transform;
                    EmptyStation e_station = e_stationObject.GetComponent<EmptyStation>();
                    emptystations.Add(e_station);


                }
                else if (obj.type == "CapStation")
                {
                    Vector3 processLoc = new Vector3(obj.position.x, 0.1f, obj.position.z);
                    processLoc += transform.position;
                    Quaternion rotation = Quaternion.Euler(new Vector3(obj.rotation.x, obj.rotation.y, obj.rotation.z));

                    GameObject e_stationObject = Instantiate(EmptyStation, processLoc, rotation, transform);
                    //b_stationObject.transform.parent = transform;
                    EmptyStation e_station = e_stationObject.GetComponent<EmptyStation>();
                    emptystations.Add(e_station);
                }
                /*
                else if (obj.type == "Product")
                {
                   
                    Vector3 processLoc = baseStationDictionary[bStationInstID].GetProcessLocation();
                    List<int> r_task = new List<int>();
                    foreach (var t in obj.ring_elements)
                    {
                        r_task.Add(t);
                    }
                    int[] currCharac = new int[3 + r_task.Count];
                    currCharac[0] = float.Parse(obj.starting_time);
                    currCharac[1] = r_task.Count;
                    for (int i = 0; i < r_task.Count; i++)
                    {
                        currCharac[2 + i] = r_task[i];
                    }
                    currCharac[-1] = int.Parse(obj.cap_element);
                    productsCharacteristics.Add(currCharac);
                    Instantiate(productObject, processLoc, Quaternion.identity, baseStationDictionary[bStationInstID].transform);
                }
                */



            }
        }
    }


    public void CollectStations(StationList myStations)
    {
        //myStations = JsonUtility.FromJson<ObjectList>(pathStation);
        if (myStations.stations.Length>0)
        {
            foreach (var obj in myStations.stations)
            {
                if (obj.type == "BaseStation")
                {
                    Vector3 processLoc = new Vector3(obj.position.x, 0.1f, obj.position.z);
                    processLoc += transform.position;
                    Quaternion rotation = Quaternion.Euler(new Vector3(obj.rotation.x, obj.rotation.y, obj.rotation.z));

                    GameObject b_stationObject = Instantiate(BaseStation, processLoc, rotation,transform);
                    //b_stationObject.transform.parent = transform;
                    BaseStation b_station = b_stationObject.GetComponent<BaseStation>();
                    baseStationDictionary.Add(b_station.ID, b_station);
                    bStationInstID = b_station.ID;
                }
                else if (obj.type == "DeliveryStation")
                {
                    Vector3 processLoc = new Vector3(obj.position.x, 0.1f, obj.position.z);
                    processLoc += transform.position;
                    Quaternion rotation = Quaternion.Euler(obj.rotation.x, obj.rotation.y, obj.rotation.z);

                    GameObject d_stationObject = Instantiate(DeliveryStation, processLoc, rotation, transform);
                    Workstation d_station = d_stationObject.GetComponent<Workstation>();
                    dStationInstID = d_station.ID;
                }
                else if (obj.type == "RingStation")
                {
                    Vector3 processLoc = new Vector3(obj.position.x, 0.1f, obj.position.z);
                    processLoc += transform.position;
                    Quaternion rotation = Quaternion.Euler(obj.rotation.x, obj.rotation.y, obj.rotation.z);


                    GameObject r_stationObject = Instantiate(RingStation, processLoc, rotation, this.transform);
                    RingStation r_station = r_stationObject.GetComponent<RingStation>();
                    foreach( var t in obj.ringtasks)
                    {
                        ringstationsTasks.Add(t.taskNumber[0], r_station.ID);
                        combProdRequired.Add(t.taskNumber[0], t.taskNumber[1]);
                    }


                }
                else if (obj.type == "CapStation" )
                {
                    Vector3 processLoc = new Vector3(obj.position.x, 0.1f, obj.position.z);
                    processLoc += transform.position;
                    Quaternion rotation = Quaternion.Euler(obj.rotation.x, obj.rotation.y, obj.rotation.z);

                    GameObject c_stationObject = Instantiate(CapStation, processLoc, rotation, this.transform);
                    CapStation c_station = c_stationObject.GetComponent<CapStation>();
                    capstationsTasks.Add(obj.capTask, c_station.ID);
                }
                /*
                else if (obj.type == "Product")
                {
                   
                    Vector3 processLoc = baseStationDictionary[bStationInstID].GetProcessLocation();
                    List<int> r_task = new List<int>();
                    foreach (var t in obj.ring_elements)
                    {
                        r_task.Add(t);
                    }
                    int[] currCharac = new int[3 + r_task.Count];
                    currCharac[0] = float.Parse(obj.starting_time);
                    currCharac[1] = r_task.Count;
                    for (int i = 0; i < r_task.Count; i++)
                    {
                        currCharac[2 + i] = r_task[i];
                    }
                    currCharac[-1] = int.Parse(obj.cap_element);
                    productsCharacteristics.Add(currCharac);
                    Instantiate(productObject, processLoc, Quaternion.identity, baseStationDictionary[bStationInstID].transform);
                }
                */



            }
        }
    }

    public void CollectChilds()
    {
        childs = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.CompareTag("product"))
            {
                childs.Add(child);
                Product product = child.GetComponent<Product>();
                products.Add(product);
            }
            else if (child.CompareTag("table"))
            {
                childs.Add(child);
                TemporalStation table = child.GetComponent<TemporalStation>();
                tables.Add(table);
            }
            else if (child.CompareTag("basestation"))
            {
                childs.Add(child);
                Workstation workstation = child.GetComponent<Workstation>();
                workstations.Add(workstation);
                BaseStation basestation = child.GetComponent<BaseStation>();
                basestations.Add(basestation);
            }
            else if (child.CompareTag("ringstation") || child.CompareTag("capstation") || child.CompareTag("deliverystation"))
            {
                childs.Add(child);
                Workstation workstation = child.GetComponent<Workstation>();
                workstations.Add(workstation);
            }
        }
    }
    // Reset all the scene
    public void ResetScene()
    {
        foreach (Transform child in childs)
        {
            // Reset Products
            if (child.CompareTag("product"))
            {
                Product temp_product = child.GetComponent<Product>();
                temp_product.EpisodeReset();
            }
            // Reset tables
            else if (child.CompareTag("table"))
            {
                TemporalStation temp_st = child.GetComponent<TemporalStation>();
                temp_st.EpisodeReset();
            }
            // Reset Workstation
            else if (child.CompareTag("basestation") || child.CompareTag("ringstation") || child.CompareTag("capstation") || child.CompareTag("deliverystation"))
            {
                Workstation temp_w = child.GetComponent<Workstation>();
                temp_w.EpisodeReset();
            }
        }
    }

    public Dictionary<int, int> GetRingstationsTasks()
    {
        return ringstationsTasks;
    }

    public Dictionary<int, int> GetCapstationsTask()
    {
        return capstationsTasks;
    }

    public Dictionary<float, Vector3> GetStationsPosition()
    {
        return stationsPosition;
    }

    public List<float[]> GetProductsCharacteristics()
    {
        return productsCharacteristics;
    }
    
    public Dictionary<int,int> GetCombProdRequired()
    {
        return combProdRequired;
    }
}

