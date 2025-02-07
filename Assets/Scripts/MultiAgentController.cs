using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using UnityEngine.AI;
using System.Linq;
using System.Text;
using System.IO;
using System;
using System.Globalization;

public class MultiAgentController : MonoBehaviour
{
    private FASController contFAS;
    private FASinfo fas_info;

    // Lists of all the elements within FAS
    private List<Transform> childs;
    private List<TemporalStation> tables;
    private Dictionary<int, Workstation> workStationDictionary;
    private Dictionary<int, Transform> inputLocationDictionary;
    private Dictionary<int, Transform> outputLocationDictionary;
    public Dictionary<int, BaseStation> baseStationDictionary;
    public Dictionary<int, RingStation> ringStationDictionary;
    public Dictionary<int, CapStation> capStationDictionary;
    public Dictionary<int, DeliveryStation> deliveryStationDictionary;
    private Dictionary<int, int> ringstationsTasks;
    private List<EmptyStation> emptyStations;
    private List<float[]> productsCharacteristics;
    private List<float>[] productsTimes;
    public int bStationInstID = 0;
    private int n_rings = 0;

    /*
    private Dictionary<int, Transform> inputBaseLocationDictionary;
    private Dictionary<int, Transform> outputBaseLocationDictionary;
    private Dictionary<int, Transform> inputRingLocationDictionary;
    private Dictionary<int, Transform> outputRingLocationDictionary;
    private Dictionary<int, Transform> inputCapLocationDictionary;
    private Dictionary<int, Transform> outputCapLocationDictionary;
    private Dictionary<int, Transform> deliveryLocationDictionary;
    */
    private Dictionary<int, Product> productDictionary;
    private Dictionary<int, List<float>> JSSPAnswer;
    private int mWaitng = -1;
    public int[] waitingList;
    public int[] combList;
    private Dictionary<int, int> combProdRequired;

    public Dictionary<int,JSSPMultiAgent> agents;
    private SimpleMultiAgentGroup AGVgroup;
    private Dictionary<(int, int), float> stationDistances;
    private NavMeshPath path;
    public GameObject scheduleObject;
    private Schedule schedule;
    private int finishedProducts=0;
    private float startTime = 0;

    private int n_products;
    public GameObject productObject;

    private float grabingTime = 0f;
    private List<Color> color_list;

    [Tooltip("Maximum steps for every episode")]
    private int maxEpisodeSteps=120000;
    private int reset_timer;
    private float speed;
    private float maxEpisodeTime = 2400f;
    private float minZ;
    private float maxZ;
    private float minX;
    private float maxX;
    private float maxD;

    [Header("Debug")]
    public List<float> lower_bounds;
    public int debbugProductID = 1;
    public float reward = 0;
    public float cummulativeReward = 0;
    public List<float> inputs;
    public bool printJobs;
    private bool printMakespan = false;
    private int instanceNum = 0;
    public bool verbose = false;
    public int episode = 0;
    public bool allJobFinished = false;
    List<float> prev_observations;
    private int jobSeed = 0;
    private bool randomness = false;
    private float lbDroppingTime;
    private float lbGrabingTime;

    //Station Observations
    List<float> stationObservation;
    float timeLastLB;

    // Rewards
    private float episode_start;
    private float h = 0;
    private float last_h = 0;
    private float initial_h;
    //private ExcelManager excelManager;

    //Result Logs
    private float bestMakespan =-1;

    private StatsRecorder statsRecorder;
    private int trainingStep;
    private StreamWriter makespanWriter;
    private bool deadlock = false;
    private int[] deadlockProds;


    void Awake()
    {
        // Initialize
        episode = 1;
        childs = new List<Transform>();
        tables = new List<TemporalStation>();
        startTime = Time.time;
        timeLastLB = startTime;
        // Set Color
        fas_info = GetComponentInParent<FASinfo>();
        color_list = fas_info.color_list;
        printMakespan = fas_info.printMakespsan;
        instanceNum = fas_info.instanceNum;
        jobSeed = fas_info.jobSeed;
        randomness = fas_info.randomness;
        lbDroppingTime = fas_info.droppingTimeLB;
        lbGrabingTime = fas_info.grabbingTimeLB;
        speed = fas_info.agent_speed;
        maxEpisodeTime = fas_info.maxEpisodeTime;
        //speed = 20f;
        //basestations = new List<BaseStation>();
        baseStationDictionary = new Dictionary<int, BaseStation>();
        ringStationDictionary= new Dictionary<int, RingStation>();
        capStationDictionary = new Dictionary<int, CapStation>();
        deliveryStationDictionary = new Dictionary<int, DeliveryStation>();
        workStationDictionary = new Dictionary<int, Workstation>();
        inputLocationDictionary = new Dictionary<int, Transform>();
        outputLocationDictionary = new Dictionary<int, Transform>();
        productDictionary = new Dictionary<int, Product>();
        emptyStations = new List<EmptyStation>();
        JSSPAnswer = new Dictionary<int, List<float>>();
        combProdRequired = new Dictionary<int, int>();

        deadlockProds = new int[2];


        agents = new Dictionary<int, JSSPMultiAgent>();
        stationDistances = new Dictionary<(int, int), float>();
        AGVgroup = new SimpleMultiAgentGroup();
        path = new NavMeshPath();
        lower_bounds = new List<float>();
        inputs = new List<float>();
        statsRecorder = Academy.Instance.StatsRecorder;
        if (scheduleObject) { schedule = scheduleObject.transform.GetComponent<Schedule>(); }
        trainingStep = 0;
        statsRecorder.Add("TrainingStep", trainingStep, StatAggregationMethod.MostRecent);



        minZ = -4f;
        maxZ = 4f;
        minX = -7f;
        maxX = 7f;
        maxD = (float) Math.Sqrt(Mathf.Pow((maxZ - minZ),2) + Mathf.Pow((maxX - minX),2));

}
    public Dictionary<int,BaseStation> GetBaseStationDictionary() {
        return baseStationDictionary;

    }
    public int NumAvailable(int ind)
    {
        return combList[ind - 1];
    }
    public int NumWaiting(int ind)
    {
        return waitingList[ind-1];
    }

    public void IncrWaiting(int ind)
    {
        combList[ind - 1]++;
    }

    public void ActivateWaiting(int ind, int num_Comb)
    {
        waitingList[ind - 1] += num_Comb;
    }
    
    public void DesactivateWaiting(int ind, int num_Comb)
    {
        waitingList[ind - 1] -= num_Comb;
    }
    
    public int[] GetWaitingList()
    {
        return waitingList;
    }
    
    public void PrintJobsCSV()
    {
        //StreamWriter writer = new StreamWriter(Application.dataPath + "/Data/" + "Jobs-" + transform.GetComponent<FASinfo>().jobSeed+".csv");
        StreamWriter writer = new StreamWriter(Application.dataPath + "/Data/" + "Jobs-"  + ".csv");

        for (int i = 0; i < productDictionary.Count; i++)
        {
            string line= "J" + productDictionary[i + 1].product_ID + ", ";
            List<Vector3> temp_job = productDictionary[i + 1].GetJob();
            for (int j = 0; j < temp_job.Count-1; j++)
            {
                line += temp_job[j].x + ", " + temp_job[j].y + ", ";
            }
            writer.WriteLine(line);

        }
        writer.Flush();
        writer.Close();
    }

    public bool AllProductsFinished()
    {
        if (n_products == finishedProducts)
        {
            return true;
        }
        return false;
    }
    
    public void IncreaseStep()
    {
        trainingStep++;
        statsRecorder.Add("TrainingStep", trainingStep, StatAggregationMethod.MostRecent);
    }

    public void Start()
    {
        Debug.Log("timeScale is :" + Time.timeScale);
        //instantiate the stations
        CollectChilds();
        waitingList = new int[workStationDictionary.Count];
        combList = new int[workStationDictionary.Count];
        //GenerateProducts();

        contFAS = transform.GetComponent<FASController>();
        productsCharacteristics = contFAS.GetProductsCharacteristics();
        ringstationsTasks = contFAS.GetRingstationsTasks();
        combProdRequired = contFAS.GetCombProdRequired();
        n_products = productsCharacteristics.Count;
        productsTimes = new List<float>[n_products];
        for (int i = 0; i < n_products; i++)
        {
            productsTimes[i] = new List<float>();
        }


        GenerateProducts2();

        Product[] products = transform.GetComponentsInChildren<Product>();
        foreach(Product p in products)
        {
            productDictionary.Add(p.product_ID, p);
        }

        debbugProductID = products[0].product_ID;
        for (int i = 0; i < productDictionary[debbugProductID].GetJob().Count; i++)
        {
            lower_bounds.Add(0f);
        }

        stationDistances = agents[1].CalculateDistances();

        

        GetInitialLB();
        initial_h = h;
        
        reward = 0;
        cummulativeReward = 0;
        episode_start = Time.time;

        if (printJobs) { PrintJobsCSV(); }

        if (printMakespan)
        {
            string title= "nada";
            if (agents[1].inference)
            {
                //title = Application.dataPath + "/Data/" + "NN-" + transform.GetComponent<FASinfo>().jobSeed + ".csv";
                title = Application.dataPath + "/Data/" + "NN" + agents.Count +"-v"+fas_info.agent_speed+"dg"+fas_info.grabbingTimeUB+'s'+fas_info.jobSeed + "start" + fas_info.startingInst +  ".csv";
            }
            else if (agents[1].SPT)
            {
                //title = Application.dataPath + "/Data/" + "SPT-" + transform.GetComponent<FASinfo>().jobSeed + ".csv";
                title = Application.dataPath + "/Data/" + "SPT"+ agents.Count + "-v" + fas_info.agent_speed + "dg" + fas_info.grabbingTimeUB + 's' + fas_info.jobSeed + "start" + fas_info.startingInst + ".csv";
            }
            else if (agents[1].LPT)
            {
                //title = Application.dataPath + "/Data/" + "LPT-" + transform.GetComponent<FASinfo>().jobSeed + ".csv";
                String name = 
                title = Application.dataPath + "/Data/" + "LPT"+ agents.Count + "-v" + fas_info.agent_speed + "dg" + fas_info.grabbingTimeUB + 's' + fas_info.jobSeed + "start" +fas_info.startingInst + ".csv";
            }
            makespanWriter = new StreamWriter(title);
        }
    }

    public void DesactivateAllElements()
    {
        foreach (var estation in emptyStations)
        {
            DestroyImmediate(estation.transform.gameObject);
        }
        foreach(KeyValuePair<int, BaseStation> kvp in baseStationDictionary)
        {
            kvp.Value.TimeToReset();
            CombProduct[] combProds = GetComponentsInChildren<CombProduct>();
            foreach (CombProduct c in combProds)
            {
                //c.Desactivate();
                DestroyImmediate(c.transform.gameObject);
            }
            // in case of a bug
            Product[] Prods = GetComponentsInChildren<Product>();
            foreach (Product p in Prods)
            {
                //p.Desactivate();
                DestroyImmediate(p.transform.gameObject);
            }
            //kvp.Value.Desactivate();
            DestroyImmediate(kvp.Value.transform.gameObject);
        }

        foreach (KeyValuePair<int, RingStation> kvp in ringStationDictionary)
        {
            CombProduct[] combProds = GetComponentsInChildren<CombProduct>();
            foreach (CombProduct c in combProds)
            {
                //c.Desactivate();
                DestroyImmediate(c.transform.gameObject);
            }
            // in case of a bug
            Product[] Prods = GetComponentsInChildren<Product>();
            foreach (Product p in Prods)
            {
                //p.Desactivate();
                DestroyImmediate(p.transform.gameObject);
            }
            //kvp.Value.Desactivate();
            DestroyImmediate(kvp.Value.transform.gameObject);
        }
        foreach (KeyValuePair<int, CapStation> kvp in capStationDictionary)
        {
            CombProduct[] combProds = GetComponentsInChildren<CombProduct>();
            foreach (CombProduct c in combProds)
            {
                //c.Desactivate();
                DestroyImmediate(c.transform.gameObject);
            }
            // in case of a bug
            Product[] Prods = GetComponentsInChildren<Product>();
            foreach (Product p in Prods)
            {
                //p.Desactivate();
                DestroyImmediate(p.transform.gameObject);
            }
            //kvp.Value.Desactivate();
            DestroyImmediate(kvp.Value.transform.gameObject);
        }
        foreach (KeyValuePair<int, DeliveryStation> kvp in deliveryStationDictionary)
        {
            CombProduct[] combProds = GetComponentsInChildren<CombProduct>();
            foreach (CombProduct c in combProds)
            {
                //c.Desactivate();
                DestroyImmediate(c.transform.gameObject);
            }
            // in case of a bug
            Product[] Prods = GetComponentsInChildren<Product>();
            foreach (Product p in Prods)
            {
                //p.Desactivate();
                DestroyImmediate(p.transform.gameObject);
            }
            //kvp.Value.Desactivate();
            DestroyImmediate(kvp.Value.transform.gameObject);
        }
        // desactivate alse all other elements in case of a bug????

        foreach (Transform child in transform)
        {
            // Check if the child has the tag 'Station'
            if (!child.CompareTag("MainCamera") && !child.CompareTag("camera") && !child.CompareTag("shop_floor") && ! child.CompareTag("AGV"))
            {
                //child.gameObject.SetActive(false);
                DestroyImmediate(child.gameObject);
            }
        }

    }

    public void Reset2()
    {
        //desactivate all previous stations and combElements and also all other elements in case where a bug happened in the previous instance
        Debug.Log("Reseting");
        DesactivateAllElements();
        // set all variables to empty
        childs = new List<Transform>();
        tables = new List<TemporalStation>();
        startTime = Time.time;
        timeLastLB = startTime;
        // Set Color
        color_list = fas_info.color_list;
        jobSeed = fas_info.jobSeed;

        //basestations = new List<BaseStation>();
        baseStationDictionary = new Dictionary<int, BaseStation>();
        ringStationDictionary = new Dictionary<int, RingStation>();
        capStationDictionary = new Dictionary<int, CapStation>();
        deliveryStationDictionary = new Dictionary<int, DeliveryStation>();
        workStationDictionary = new Dictionary<int, Workstation>();
        inputLocationDictionary = new Dictionary<int, Transform>();
        outputLocationDictionary = new Dictionary<int, Transform>();
        productDictionary = new Dictionary<int, Product>();
        emptyStations = new List<EmptyStation>();
        JSSPAnswer = new Dictionary<int, List<float>>();
        combProdRequired = new Dictionary<int, int>();
        productsTimes = new List<float>[n_products];
        deadlockProds = new int[2]; 
        for (int i = 0; i < n_products; i++)
        {
            productsTimes[i]= new List<float>();
        }

        //agents = new Dictionary<int, JSSPMultiAgent>();
        stationDistances = new Dictionary<(int, int), float>();
        //AGVgroup = new SimpleMultiAgentGroup();
        //path = new NavMeshPath();
        lower_bounds = new List<float>();
        inputs = new List<float>();
        statsRecorder = Academy.Instance.StatsRecorder;
        if (scheduleObject) { schedule = scheduleObject.transform.GetComponent<Schedule>(); }
        IncreaseStep();
        deadlock = false;
        contFAS.EpisodeReset();
        /*
        //inst machine
        string pathStation = Path.Combine("D:\\111_Work\\unity\\extracted\\Cstation", "Cstation3.json");
        contFAS.InstStations(pathStation);

        // instantiate new ones and add variables as expecteds
        string pathProduct = Path.Combine("D:\\111_Work\\unity\\extracted\\product", "product3.json");
        contFAS.InstProducts(pathProduct);
        */
        contFAS.NextInst();
        contFAS.Rebake();

        CollectChildsAfterReset();
        waitingList = new int[workStationDictionary.Count];
        combList = new int[workStationDictionary.Count];
        //GenerateProducts();

        //contFAS = transform.GetComponent<FASController>();
        productsCharacteristics = contFAS.GetProductsCharacteristics();
        ringstationsTasks = contFAS.GetRingstationsTasks();
        combProdRequired = contFAS.GetCombProdRequired();
        n_products = productsCharacteristics.Count;
        finishedProducts = 0;
        GenerateProducts2();

        Product[] products = transform.GetComponentsInChildren<Product>();
        foreach (Product p in products)
        {
            productDictionary.Add(p.product_ID, p);
        }

        foreach(KeyValuePair<int, JSSPMultiAgent>  a in agents)
        {
            a.Value.Reset();
        }

        debbugProductID = products[0].product_ID;
        for (int i = 0; i < productDictionary[debbugProductID].GetJob().Count; i++)
        {
            lower_bounds.Add(0f);
        }

        stationDistances = agents[1].CalculateDistances();

        GetInitialLB();
        initial_h = h;

        reward = 0;
        cummulativeReward = 0;
        episode_start = Time.time;

        if (printJobs) { PrintJobsCSV(); }

    }

    public void ResetMAC()
    {
        // Initialize
        episode = 0;
        childs = new List<Transform>();
        tables = new List<TemporalStation>();
        startTime = Time.time;
        // Set Color
        FASinfo fas_info = GetComponentInParent<FASinfo>();
        color_list = fas_info.color_list;

        //basestations = new List<BaseStation>();
        baseStationDictionary = new Dictionary<int, BaseStation>();
        ringStationDictionary = new Dictionary<int, RingStation>();
        capStationDictionary = new Dictionary<int, CapStation>();
        deliveryStationDictionary = new Dictionary<int, DeliveryStation>();
        workStationDictionary = new Dictionary<int, Workstation>();
        inputLocationDictionary = new Dictionary<int, Transform>();
        outputLocationDictionary = new Dictionary<int, Transform>();
        productDictionary = new Dictionary<int, Product>();
        JSSPAnswer = new Dictionary<int, List<float>>();
        combProdRequired = new Dictionary<int, int>();

        agents = new Dictionary<int, JSSPMultiAgent>();
        stationDistances = new Dictionary<(int, int), float>();
        AGVgroup = new SimpleMultiAgentGroup();
        path = new NavMeshPath();
        lower_bounds = new List<float>();
        inputs = new List<float>();
        statsRecorder = Academy.Instance.StatsRecorder;
        if (scheduleObject) { schedule = scheduleObject.transform.GetComponent<Schedule>(); }
        trainingStep = 0;
        combProdRequired.Add(4, 1);
        combProdRequired.Add(5, 2);
        //instantiate the stations
        CollectChilds();
        waitingList = new int[workStationDictionary.Count];
        combList = new int[workStationDictionary.Count];
        //GenerateProducts();

        contFAS = transform.GetComponent<FASController>();
        productsCharacteristics = contFAS.GetProductsCharacteristics();
        GenerateProducts2();

        Product[] products = transform.GetComponentsInChildren<Product>();
        foreach (Product p in products)
        {
            productDictionary.Add(p.product_ID, p);
        }

        debbugProductID = products[0].product_ID;
        for (int i = 0; i < productDictionary[debbugProductID].GetJob().Count; i++)
        {
            lower_bounds.Add(0f);
        }

        stationDistances = agents[1].CalculateDistances();



        GetInitialLB();
        initial_h = h;

        reward = 0;
        cummulativeReward = 0;
        episode_start = Time.time;

        if (printJobs) { PrintJobsCSV(); }

        if (printMakespan)
        {
            string title = "nada";
            if (agents[1].inference)
            {
                //title = Application.dataPath + "/Data/" + "NN-" + transform.GetComponent<FASinfo>().jobSeed + ".csv";
                title = Application.dataPath + "/Data/" + "NN-" + ".csv";
            }
            else if (agents[1].SPT)
            {
                //title = Application.dataPath + "/Data/" + "SPT-" + transform.GetComponent<FASinfo>().jobSeed + ".csv";
                title = Application.dataPath + "/Data/" + "SPT-" + ".csv";
            }
            else if (agents[1].LPT)
            {
                //title = Application.dataPath + "/Data/" + "LPT-" + transform.GetComponent<FASinfo>().jobSeed + ".csv";
                title = Application.dataPath + "/Data/" + "LPT-" + ".csv";
            }
            makespanWriter = new StreamWriter(title);
        }
    }

    public void GenerateProducts2()
    {
        for (int i = 0; i < productsCharacteristics.Count; i++)
        {
            //int k = basestations.Count + 1;
            int temp;
            temp = 1;   //UnityEngine.Random.Range(1, 3);
            Vector3 processLoc;
            processLoc = baseStationDictionary[temp].GetProcessLocation();
            Instantiate(productObject, processLoc, Quaternion.identity, baseStationDictionary[temp].transform);
        }
    }

    public void GenerateProducts()
    {
        for (int i = 0; i < n_products; i++)
        {
            //int k = basestations.Count + 1;
            int temp;
            temp = 0;   //UnityEngine.Random.Range(1, 3);
            Vector3 processLoc;
            processLoc = baseStationDictionary[temp].GetProcessLocation();
            Instantiate(productObject, processLoc, Quaternion.identity,baseStationDictionary[temp].transform);
        }
    }


    public void GetInitialLB()
    {
        h = 0;
        for (int i = 1; i < productDictionary.Count + 1; i++)
        {
            Product temp_p = productDictionary[i];
            float lower_bound = 0;
            int temp_ID = temp_p.product_ID;
            List<Vector3> product_job = temp_p.GetJob();
            int curr_machine = (int)temp_p.GetCurrentTask().x;
            int nRings = (int)productsCharacteristics[i - 1][1];

            if (temp_p.task_pointer == 0)
            {
                //float currentTaskTime = float.MaxValue;
                //foreach (KeyValuePair<int,JSSPMultiAgent> agent in agents)
                //{
                //    float temp_task_time = 0;
                //    NavMesh.CalculatePath(agent.Value.transform.position, baseStationDictionary[1].output_location.position, NavMesh.AllAreas, path);
                //    for (int j = 0; j < path.corners.Length - 1; j++)
                //    {
                //        temp_task_time += Vector3.Distance(path.corners[j], path.corners[j + 1]) / speed;

                //    }
                //    if (temp_task_time < currentTaskTime)
                //    {
                //        currentTaskTime = temp_task_time;
                //    }
                //}
                //lower_bound += currentTaskTime;
                float expectedTime = baseStationDictionary[1].ReadyTime();
                expectedTime = Mathf.Max(expectedTime, temp_p.starting_time);
                lower_bound = Mathf.Max(lower_bound,expectedTime);
                h = Mathf.Max(h, lower_bound);
                float dist;

                for (int k = temp_p.task_pointer + 1; k < product_job.Count; k++)
                {
                    dist = 0;
                    dist = stationDistances[((int)product_job[k - 1].x, (int)product_job[k].x)];
                    
                        
                    lower_bound += lbDroppingTime + lbDroppingTime + (dist / speed)  + product_job[k].y;
                    //if (k <= nRings)
                    //{
                    //    lower_bound += (float)combProdRequired[(int)productsCharacteristics[temp_ID - 1][1 + k]] * 
                    //        stationDistances[((int)baseStationDictionary.First().Key,(int)product_job[k].x)]/speed;
                    //}
                    if (temp_ID == debbugProductID)
                    {
                        lower_bounds[i] = lower_bound;
                    }
                    h = Mathf.Max(h, lower_bound);
                }
            }
            
            else if (temp_p.task_pointer < product_job.Count)
            {
                // Current Task
                float currentTaskTime = 0;

                //Time of product to station

                NavMesh.CalculatePath(temp_p.transform.position, inputLocationDictionary[curr_machine].position, NavMesh.AllAreas, path);

                for (int j = 0; j < path.corners.Length - 1; j++)
                {
                    currentTaskTime += Vector3.Distance(path.corners[j], path.corners[j + 1]) / speed;

                }

                lower_bound += lbDroppingTime + lbDroppingTime + currentTaskTime + product_job[temp_p.task_pointer].y;
                //if (temp_p.task_pointer <= nRings)
                //{
                //    lower_bound += (float)combProdRequired[(int)productsCharacteristics[temp_ID - 1][1 + temp_p.task_pointer]] *
                //        stationDistances[((int)baseStationDictionary.First().Key, (int)product_job[temp_p.task_pointer].x)]/speed;
                //}
                if (temp_ID == debbugProductID)
                {
                    lower_bounds[temp_p.task_pointer] = lower_bound;
                }
                h = Mathf.Max(h, lower_bound);
                float dist;

                for (int k = temp_p.task_pointer+1; k < product_job.Count; k++)
                {
                    dist = 0;
                    dist = stationDistances[((int)product_job[k-1].x, (int)product_job[k].x)];
                    if (k <= nRings)
                    {
                        //lower_bound += (float)combProdRequired[(int)productsCharacteristics[temp_ID - 1][1 + k]] *
                        //    stationDistances[((int)baseStationDictionary.First().Key, (int)product_job[k].x)]/speed;
                    }
                    lower_bound += lbDroppingTime + lbDroppingTime + dist / speed + product_job[k].y;
                    if (temp_ID == debbugProductID)
                    {
                        lower_bounds[i] = lower_bound;
                    }
                    h = Mathf.Max(h, lower_bound);
                }
            }
        }
        int debu = 0;
        //Debug.Log("Initial: " + h);
    }



    
    public void ProductFinished()
    {
        finishedProducts += 1;
    }

    private void FixedUpdate()
    {
        reset_timer += 1;
        if ((reset_timer > maxEpisodeSteps && maxEpisodeSteps > 0) && !AllProductsFinished())
        {
            float f_t = Time.time - startTime;
            Debug.Log("instance stoped after " + f_t);
            Debug.Log("cummulative Reward is:" + cummulativeReward);
            Reset2();
            reset_timer = 0;
            episode++;
            AGVgroup.GroupEpisodeInterrupted();
            episode_start = Time.time;
            reward = 0;
            cummulativeReward = 0;

            h = initial_h;
        }
        if (AllProductsFinished())
        {
            float f_t = Time.time - startTime;
            Debug.Log("instance finished after " + f_t);
            Debug.Log("cummulative Reward is:" + cummulativeReward);
            //Debug.Log("n_zeros of agent 1 is: " + agents[1].n_zeros);
            //Debug.Log("n_zeros of agent 2 is: " + agents[2].n_zeros);
            if (jobSeed > 0 )
            {
                Debug.Log("seed :" + jobSeed);
            }
            float temp2 = Time.time - episode_start;
            reset_timer = 0;
            //Debug.Log("Makespan= " + temp2 + " CR= " + agents[1].GetCumulativeReward());
            if (printMakespan)
            {
                string line = "";

                line += episode.ToString() + ", ";

                line += temp2.ToString(CultureInfo.InvariantCulture);
                line+=',';
                line += cummulativeReward.ToString(CultureInfo.InvariantCulture);
                line+= ',';
                Debug.Log(line);
                makespanWriter.WriteLine(line);
            }
            episode++;
            if (bestMakespan < 0)
            {
                bestMakespan = h;


            }
            //Get Best Schedule
            if (bestMakespan > h)
            {
                bestMakespan = h;
                for (int i = 0; i < productDictionary.Count; i++)
                {
                    var p = productDictionary[i + 1];
                    List<float> temp = p.GetTimes();
                    JSSPAnswer[p.product_ID] = temp;
                }
                if (schedule)
                {
                    schedule.ProposeMakespan(bestMakespan);
                }
            }
            statsRecorder.Add("JSSP/AverageMakespan", h);

            AGVgroup.EndGroupEpisode();
            Reset2();
            reset_timer = 0;
            episode_start = Time.time;
            reward = 0;
            cummulativeReward = 0;
            h = initial_h;
        }
    }

    private void Fixed2()
    {

        reset_timer += 1;
        if ((reset_timer>maxEpisodeSteps && maxEpisodeSteps > 0) || AllProductsFinished())
        {
            ResetScene();
            reset_timer = 0;
            AGVgroup.GroupEpisodeInterrupted();
            episode_start = Time.time;
            reward = 0;
            cummulativeReward = 0;
            
            h = initial_h;
            if (bestMakespan > h)
            {
                bestMakespan = h;
                for (int i = 0; i < productDictionary.Count; i++)
                {
                    var p = productDictionary[i + 1];
                    List<float> temp = p.GetTimes();
                    JSSPAnswer[p.product_ID] = temp;
                }
                if (schedule)
                {
                    schedule.ProposeMakespan(bestMakespan);
                }
            }
            statsRecorder.Add("JSSP/AverageMakespan", h);

            AGVgroup.EndGroupEpisode();
            ResetScene();
            reset_timer = 0;
            episode_start = Time.time;
            reward = 0;
            cummulativeReward = 0;
            h = initial_h;
        }

        bool finished = true;
        for (int i = 1; i < productDictionary.Count + 1; i++) { finished = finished && productDictionary[i].finished; }
        if (finished)
        {
            float temp2 = Time.time - episode_start;
            //Debug.Log("Makespan= " + temp2 + " CR= " + agents[1].GetCumulativeReward());
            if (printMakespan)
            {
                string line = "";
               
                line += episode +", ";

                line += temp2;
                Debug.Log(line);
                makespanWriter.WriteLine(line);
            }
            episode++;
            if(bestMakespan < 0) 
            { 
                bestMakespan = h;
                
                
            }
            //Get Best Schedule
            if (bestMakespan > h)
            {
                bestMakespan = h;
                for (int i = 0; i < productDictionary.Count; i++)
                {
                    var p = productDictionary[i + 1];
                    List<float> temp = p.GetTimes();
                    JSSPAnswer[p.product_ID] = temp;
                }
                if (schedule)
                {
                    schedule.ProposeMakespan(bestMakespan);
                }
            }
            statsRecorder.Add("JSSP/AverageMakespan", h);

            AGVgroup.EndGroupEpisode();
            ResetScene();
            reset_timer = 0;
            episode_start = Time.time;
            reward = 0;
            cummulativeReward = 0;
            h = initial_h;

        }
    }

    public void CollectChildsAfterReset()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.CompareTag("product"))
            {
                childs.Add(child);
                Product product = child.GetComponent<Product>();
                productDictionary.Add(product.product_ID, product);
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
                Transform input = workstation.input_location;
                Transform output = workstation.output_location;
                inputLocationDictionary.Add(workstation.ID, input);
                outputLocationDictionary.Add(workstation.ID, output);
                workStationDictionary.Add(workstation.ID, workstation);
                BaseStation basestation = child.GetComponent<BaseStation>();
                baseStationDictionary.Add(basestation.base_ID, basestation);
                bStationInstID = workstation.ID;
            }
            else if (child.CompareTag("ringstation"))
            {
                childs.Add(child);
                Workstation workstation = child.GetComponent<Workstation>();
                Transform input = workstation.input_location;
                Transform output = workstation.output_location;
                inputLocationDictionary.Add(workstation.ID, input);
                outputLocationDictionary.Add(workstation.ID, output);
                workStationDictionary.Add(workstation.ID, workstation);
                RingStation ringstation = child.GetComponent<RingStation>();
                ringStationDictionary.Add(ringstation.ID, ringstation);
            }
            else if (child.CompareTag("capstation"))
            {
                childs.Add(child);
                Workstation workstation = child.GetComponent<Workstation>();
                Transform input = workstation.input_location;
                Transform output = workstation.output_location;
                inputLocationDictionary.Add(workstation.ID, input);
                outputLocationDictionary.Add(workstation.ID, output);
                workStationDictionary.Add(workstation.ID, workstation);
                CapStation capstation = child.GetComponent<CapStation>();
                capStationDictionary.Add(capstation.ID, capstation);
            }
            else if (child.CompareTag("emptystation"))
            {
                childs.Add(child);
                EmptyStation estation = child.GetComponent<EmptyStation>();
                emptyStations.Add(estation);
            }
            else if (child.CompareTag("deliverystation"))
            {
                childs.Add(child);
                Workstation workstation = child.GetComponent<Workstation>();
                Transform input = workstation.input_location;
                Transform output = workstation.output_location;
                inputLocationDictionary.Add(workstation.ID, input);
                outputLocationDictionary.Add(workstation.ID, output);
                workStationDictionary.Add(workstation.ID, workstation);
                DeliveryStation deliverystation = child.GetComponent<DeliveryStation>();
                deliveryStationDictionary.Add(deliverystation.ID, deliverystation);
            }
        }
    }

    public void CollectChilds()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.CompareTag("product"))
            {
                childs.Add(child);
                Product product = child.GetComponent<Product>();
                productDictionary.Add(product.product_ID, product);
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
                Transform input = workstation.input_location;
                Transform output = workstation.output_location;
                inputLocationDictionary.Add(workstation.ID, input);
                outputLocationDictionary.Add(workstation.ID, output);
                workStationDictionary.Add(workstation.ID, workstation);
                BaseStation basestation = child.GetComponent<BaseStation>();
                baseStationDictionary.Add(basestation.base_ID,basestation);
                bStationInstID = workstation.ID;
            }
            else if (child.CompareTag("ringstation"))
            {
                childs.Add(child);
                Workstation workstation = child.GetComponent<Workstation>();
                Transform input = workstation.input_location;
                Transform output = workstation.output_location;
                inputLocationDictionary.Add(workstation.ID, input);
                outputLocationDictionary.Add(workstation.ID, output);
                workStationDictionary.Add(workstation.ID, workstation);
                RingStation ringstation = child.GetComponent<RingStation>();
                ringStationDictionary.Add(ringstation.ID, ringstation);
            }
            else if (child.CompareTag("capstation"))
            {
                childs.Add(child);
                Workstation workstation = child.GetComponent<Workstation>();
                Transform input = workstation.input_location;
                Transform output = workstation.output_location;
                inputLocationDictionary.Add(workstation.ID, input);
                outputLocationDictionary.Add(workstation.ID, output);
                workStationDictionary.Add(workstation.ID, workstation);
                CapStation capstation = child.GetComponent<CapStation>();
                capStationDictionary.Add(capstation.ID, capstation);
            }
            else if ( child.CompareTag("deliverystation"))
            {
                childs.Add(child);
                Workstation workstation = child.GetComponent<Workstation>();
                Transform input = workstation.input_location;
                Transform output = workstation.output_location;
                inputLocationDictionary.Add(workstation.ID, input);
                outputLocationDictionary.Add(workstation.ID, output);
                workStationDictionary.Add(workstation.ID, workstation);
                DeliveryStation deliverystation = child.GetComponent<DeliveryStation>();
                deliveryStationDictionary.Add(deliverystation.ID, deliverystation);
            }
            else if (child.CompareTag("emptystation"))
            {
                childs.Add(child);
                EmptyStation estation = child.GetComponent<EmptyStation>();
                emptyStations.Add(estation);
            }
            else if (child.CompareTag("AGV"))
            {
                childs.Add(child);
                JSSPMultiAgent agent = child.GetComponent<JSSPMultiAgent>();
                AGVgroup.RegisterAgent(agent);
                agents.Add(agent.ID,agent);
                //agent.agent.speed = speed;
            }

        }
    }

    public bool CheckDeadlock()
    {
        foreach (var kvp in ringStationDictionary)
        {
            Product p = kvp.Value.GetNextProd();
            if (p && !p.grabbed && !p.assigned )//!p.blocked
            {
                int next = (int)p.GetCurrentTask().x;
                if(next == kvp.Key || !ringStationDictionary.ContainsKey(next) || kvp.Value.nProds()<2)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
            
        }
        /*
        foreach (var kvp in ringStationDictionary)
        {
            kvp.Value.UnAssign();
        }
        */
        int ind = 0;
        foreach (var kvp in ringStationDictionary)
        {
            Product p = kvp.Value.GetNextProd();
            deadlockProds[ind]=p.product_ID;
            ind += 1;
        }
        
        deadlock = true;
        return true;
    }

    public int[] getDeadlockProds()
    {
        return deadlockProds;
    }

    public void dealockProdTaken(int ind)
    {
        deadlockProds[ind] = -1;
    }

    // Reset all the scene
    public void ResetSceneInst()
    {
        foreach (Transform child in childs)
        {
            // Reset Products
            if (child.CompareTag("product"))
            {
                Product temp_product = child.GetComponent<Product>();
                temp_product.Desactivate();
            }
            // Reset tables
            else if (child.CompareTag("table"))
            {
                TemporalStation temp_st = child.GetComponent<TemporalStation>();
                temp_st.EpisodeReset();
            }
            // Reset Workstation
            else if (child.CompareTag("basestation") || child.CompareTag("ringstation") || child.CompareTag("capstation")
                || child.CompareTag("deliverystation"))
            {
                Workstation temp_w = child.GetComponent<Workstation>();
                //temp_w.EpisodeReset();
                temp_w.Desactivate();
            }
        }
        foreach (Transform child in childs)
        {
            // Reset Products
            if (child.CompareTag("combproduct"))
            {
                CombProduct temp_combproduct = child.GetComponent<CombProduct>();
                temp_combproduct.Desactivate();
            }
            // Reset tables

        }
        Debug.Log("All the object have been deactivated");
        contFAS.EpisodeReset();
        Debug.Log("Object Related to a new instance have been instantiated");
        ResetMAC();
        foreach (KeyValuePair<int, JSSPMultiAgent> a in agents)
        {
            a.Value.Reset();
        }


    }

    public void DLogs()
    {
        int k = 0;
        Debug.Log("Deadlock: " + deadlock);
        foreach (KeyValuePair<int, JSSPMultiAgent> a in agents)
        {
           
            Debug.Log("agent: " + a.Key);
            Debug.Log("carrying: " + a.Value.carrying_p);
            Debug.Log("decided" + a.Value.decided);
        }
    }

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
            else if (child.CompareTag("basestation") || child.CompareTag("ringstation") || child.CompareTag("capstation") 
                || child.CompareTag("deliverystation"))
            {
                Workstation temp_w = child.GetComponent<Workstation>();
                temp_w.EpisodeReset();
            }
        }
    }



    public List<float> GetLBObservations(int agentID, Vector3 agentPosition)
    {
        List<float> observations = new List<float>();
        last_h = h;
        h = 0;
        int nullProductCount = 0;
        float passedTime = Time.time - episode_start;
        //timeLastLB = Time.time;
        observations.Add(NormalizeTimeValues(passedTime));

        AddAgentObservations(agentID, observations);
        AddStationsObservations(observations);
        AddProductObservations(agentID, agentPosition, observations, ref nullProductCount);

        CalculateAndLogRewards();

        foreach (KeyValuePair<int, JSSPMultiAgent> agent in agents)
        {
            agent.Value.AddReward(reward);
        }

        foreach (var obs in observations)
        {
            inputs.Add(obs);
        }

        //if (observations.Count != 276)
        //{
        //    int k = 0;
        //}
        //if (last_h - h >1 || last_h -h <-9)
        //{
        //    float k = last_h - h;
        //}

        //prev_observations = observations;
        //foreach (float f in observations)
        //{
        //    if (f > 1 || f<0)
        //    {
        //        int found = 0;
        //    }
        //}
        return observations;
    }

    //private void AddStationsLocations(List<float> observations)
    //{
    //    List<float> tempObservation;
    //    Vector3 position;
    //    Vector3 rotation;
    //    foreach (var kvp in baseStationDictionary)
    //    {
    //        position = kvp.Value.transform.position;
    //        rotation = kvp.Value.transform.rotation;
    //        tempObservation.Add(position.x);
    //        tempObservation.Add(position.x);
    //        tempObservation.Add(rotation.y / 360);

    //    }
    //    position = ringStationDictionary[ringstationsTasks[2]].position;
    //    rotation = ringStationDictionary[ringstationsTasks[2]].rotation;
    //    tempObservation.Add(position.transform.x);
    //    tempObservation.Add(position.transform.x);
    //    tempObservation.Add(rotation.transform.y / 360);
        
    //    position = ringStationDictionary[ringstationsTasks[4]].position;
    //    rotation = ringStationDictionary[ringstationsTasks[4]].rotation;
    //    tempObservation.Add(position.x);
    //    tempObservation.Add(position.x);
    //    tempObservation.Add(rotation.y / 360);

        

    //}

    private void AddStationsObservations(List<float> observations)
    {
        foreach (var kvp in ringStationDictionary)
        {
            Vector3 relativePosition = this.transform.InverseTransformPoint(kvp.Value.transform.position);
            observations.Add(NormalizeXValues(relativePosition.x));
            observations.Add(NormalizeZValues(relativePosition.z));
            if (kvp.Value.waitingForComb())
            {
                observations.Add(1f);
            }
            else
            {
                observations.Add(0f);
            }
        }
    }

    private void AddAgentObservations(int agentID, List<float> observations)
    {
        //observations.Add(agentID);
        foreach (KeyValuePair<int, JSSPMultiAgent> agent in agents)
        {
            Vector3 relativePosition = this.transform.InverseTransformPoint(agent.Value.transform.position);
            if (agentID == agent.Value.ID)
            {
                observations.Add(1f);
            }
            else
            {
                observations.Add(0f);
            }
            //observations.Add(agent.Value.ID / agents.Count); //lastly added
            observations.Add(NormalizeXValues(relativePosition.x));
            observations.Add(NormalizeZValues(relativePosition.z));
            observations.Add((float)agent.Value.last_action/13f);
        }
    }

    private void AddProductObservations(int agentID, Vector3 agentPosition, List<float> observations, ref int nullProductCount)
    {
        for (int i = 1; i <= productDictionary.Count; i++)
        {
            int nRings = (int)productsCharacteristics[i - 1][1];
            if (productDictionary[i] == null)
            {
                AddNullProductObservations(i, nRings, observations, ref nullProductCount);
            }
            else
            {
                AddNonNullProductObservations(agentID, i, nRings, agentPosition, observations);
            }
        }
    }

    private void AddNullProductObservations(int index, int nRings, List<float> observations, ref int nullProductCount)
    {
        nullProductCount++;
        observations.Add(NormalizeTimeValues(productsCharacteristics[index - 1][0]));
        observations.AddRange(new float[] { 0, 0, 1, NormalizeTimeValues(productsTimes[index - 1][0])});

        for (int j = 0; j < nRings; j++)
        {
            float id = 0.5f;
            if ((int)productsCharacteristics[index - 1][2 + j]==4 || (int)productsCharacteristics[index - 1][2 + j]==5)
            {
                id = 1f;
            }
            observations.AddRange(new float[] {
            1,
            NormalizeTimeValues(productsTimes[index - 1][j + 1]),
            0f,
            id,
            (float)combProdRequired[(int)productsCharacteristics[index - 1][2 + j]]/3+0.1f,
            NormalizeDistanceValue(stationDistances[(baseStationDictionary.First().Key, ringstationsTasks[(int)productsCharacteristics[index - 1][2 + j]])])
            });
        }

        for (int j = 0; j < 3 - nRings; j++)
        {
            observations.AddRange(new float[] {0, 0, 0, 0, 0, 0 });
        }

        float idC = 0.5f;
        if ((int)productsCharacteristics[index - 1][2 + nRings] == 7)
        {
            idC = 1f;
        }

        observations.AddRange(new float[] {
            1, NormalizeTimeValues(productsTimes[index - 1][nRings + 1]),
            0,
            idC,
            1, NormalizeTimeValues(productsTimes[index - 1][nRings + 2]),
            0,
        });

        if (nullProductCount == n_products)
        {
            h = last_h;
        }
    }

    private float NormalizeTimeValues(float time)
    {
        return time / maxEpisodeTime;
    }
    private float NormalizeXValues(float x)
    {
        return (x-minX) / (maxX - minX);
    }
    private float NormalizeZValues(float z)
    {
        return (z-minZ) / (maxZ-minZ);
    }
    private float NormalizeDistanceValue(float z)
    {
        return z / maxD;
    }
    private void AddNonNullProductObservations(int agentID, int index, int nRings, Vector3 agentPosition, List<float> observations)
    {
        List<float> inputs = new List<float>();
        Product product = productDictionary[index];
        float lowerBound = Time.time - episode_start;
        int productID = product.product_ID;
        List<Vector3> productJob = product.GetJob();
        //List<float> productTimes = product.GetTimes();
        int currentMachine = (int)product.GetCurrentTask().x;
        int currentTask = (int)product.GetCurrentTask().x;

       // Vector3 temp = this.transform.InverseTransformPoint(product.transform.position); Math.Abs(distanceToProduct.x / (maxX * 2)),
        //Math.Abs(distanceToProduct.z / (maxZ * 2))
        //Vector3 temp2 = this.transform.InverseTransformPoint(agentPosition); 

        Vector3 distanceToProduct = this.transform.InverseTransformPoint(product.transform.position)  - this.transform.InverseTransformPoint(agentPosition);

        observations.AddRange(new float[] {
        NormalizeTimeValues(product.starting_time),
        NormalizeDistanceValue(Math.Abs(distanceToProduct.x)),
        NormalizeDistanceValue(Math.Abs(distanceToProduct.z))
        });
        //if(product.task_pointer == 1 && productsTimes[index - 1].Count == 0 || product.task_pointer == 0)
        //{
        //    observations.Add(1f);
        //}
        //else
        //{
        //    Workstation station = product.GetComponentInParent<Workstation>();
        //    if(!product.assigned && station && station.GetNextProd() && station.GetNextProd().product_ID==product.product_ID)//&& station.GetReadyProd()
        //    {
        //        observations.Add(1f);
        //    }
        //    //else if(!product.assigned && station && station.GetNextProd() && station.GetNextProd().product_ID == product.product_ID)
        //    //{

        //    //}
        //    else
        //    {
        //        observations.Add(0f);
        //    }
        //}

        int observationCount = 0;
        AddCompletedTaskObservations(index, nRings, product, observations, ref observationCount);

        if (product.task_pointer == 1 && productsTimes[index-1].Count==0)
        {
            //float currentTaskTime = float.MaxValue;
            //foreach (KeyValuePair<int, JSSPMultiAgent> agent in agents)
            //{
            //    float temp_task_time = 0;
            //    NavMesh.CalculatePath(agent.Value.transform.position, baseStationDictionary[1].output_location.position, NavMesh.AllAreas, path);
            //    for (int j = 0; j < path.corners.Length - 1; j++)
            //    {
            //        temp_task_time += Vector3.Distance(path.corners[j], path.corners[j + 1]) / speed;

            //    }
            //    if (temp_task_time < currentTaskTime)
            //    {
            //        currentTaskTime = temp_task_time;
            //    }
            //}
            //lowerBound += currentTaskTime;
            float expectedTime = baseStationDictionary[1].ReadyTime();
            expectedTime = Mathf.Max(expectedTime, product.starting_time);
            lowerBound = Mathf.Max(lowerBound, expectedTime);
            observations.AddRange(new float[] { 0.5f, NormalizeTimeValues(lowerBound) });
            if (0 == nRings)
            {
                AddEmptyTaskObservations(3 - nRings, observations);
            }
            //lowerBound += expectedTime;
            h = Mathf.Max(h, lowerBound);


            float dist = 0;
            dist = stationDistances[((int)productJob[0].x, (int)productJob[1].x)];

            lowerBound = lowerBound + lbGrabingTime + lbDroppingTime + (dist / speed) + productJob[1].y;
            observations.Add(0f);

            if (nRings > 0)
            {
                //lowerBound += (float)combProdRequired[(int)productsCharacteristics[product.product_ID - 1][2]] *
                //    stationDistances[((int)baseStationDictionary.First().Key, (int)product.GetJob()[1].x)] / speed;
                observations.Add(NormalizeTimeValues(lowerBound));
                observations.Add(NormalizeDistanceValue(dist));
                float id = 0.5f;
                if ((int)productsCharacteristics[product.product_ID - 1][2] == 4 || (int)productsCharacteristics[product.product_ID - 1][2] == 5)
                {
                    id = 1f;
                }
                observations.AddRange(new float[] {
                    id,
                    (float)combProdRequired[(int)productsCharacteristics[product.product_ID - 1][2]]/3+0.1f,
                    NormalizeDistanceValue(stationDistances[(baseStationDictionary.First().Key, ringstationsTasks[(int)productsCharacteristics[product.product_ID - 1][2]])])
                });
                if (1 == nRings )
                {
                    AddEmptyTaskObservations(3 - nRings, observations);
                }
            }
            else
            {
                observations.Add(NormalizeTimeValues(lowerBound));
                observations.Add(NormalizeDistanceValue(dist));
                float id = 0.5f;
                if ((int)productsCharacteristics[product.product_ID - 1][2 + nRings] == 7)
                {
                    id = 1f;
                }
                observations.AddRange(new float[] {
                    id
                });
            }
            h = Mathf.Max(h, lowerBound);
            AddFutureTaskObservations(product, productJob, lowerBound, nRings, observations);
        }
        if (product.task_pointer == 0)
        {
            //float currentTaskTime = float.MaxValue;
            //foreach (KeyValuePair<int, JSSPMultiAgent> agent in agents)
            //{
            //    float temp_task_time = 0;
            //    NavMesh.CalculatePath(agent.Value.transform.position, baseStationDictionary[1].output_location.position, NavMesh.AllAreas, path);
            //    for (int j = 0; j < path.corners.Length - 1; j++)
            //    {
            //        temp_task_time += Vector3.Distance(path.corners[j], path.corners[j + 1]) / speed;

            //    }
            //    if (temp_task_time < currentTaskTime)
            //    {
            //        currentTaskTime = temp_task_time;
            //    }
            //}
            //lowerBound += currentTaskTime;
            float expectedTime = baseStationDictionary[1].ReadyTime();
            expectedTime = Mathf.Max(expectedTime, product.starting_time);
            lowerBound = Mathf.Max(lowerBound, expectedTime);
            observations.AddRange(new float[] { 0.5f, NormalizeTimeValues(lowerBound) });
            if (0 == nRings && nRings < 3)
            {
                AddEmptyTaskObservations(3 - nRings, observations);
            }
            //lowerBound += expectedTime;
            h = Mathf.Max(h, lowerBound);
            AddFutureTaskObservations(product, productJob, lowerBound, nRings, observations);
        }

        if (product.task_pointer>0 && productsTimes[index - 1].Count > 0 && product.task_pointer < productJob.Count && !product.finished)
        {
            
            if (!product.IsBlocked())
            {
                observations.Add(0.5f);
                lowerBound = CalculateCurrentTaskTime( product, productJob, lowerBound,observations);
                
            }
            else
            {
                if (product.process_start <= 0 && product.task_pointer!=0)
                {
                    lowerBound += productJob[product.task_pointer].y;
                }
                else
                {
                    lowerBound += productJob[product.task_pointer].y - (Time.time - product.process_start);

                }
                
                observations.Add(1f);
                observations.Add(NormalizeTimeValues(lowerBound));
                observations.Add(NormalizeDistanceValue(stationDistances[((int)productJob[product.task_pointer-1].x, (int)productJob[product.task_pointer].x)]));
            }
            //if(product.task_pointer<nRings && product.task_pointer <= 3)
            //{
            //    lowerBound+=(float)combProdRequired[(int)productsCharacteristics[product.product_ID - 1][1 + product.task_pointer]] *
            //        stationDistances[((int)baseStationDictionary.First().Key, (int)product.GetJob()[product.task_pointer].x)];
            //}
            //int expectedcomb = workStationDictionary[(int)product.GetCurrentTask().x].ExpectingCombs();
            //if (expectedcomb > 0)
            //{
            //    lowerBound += expectedcomb * stationDistances[((int)baseStationDictionary.First().Key, (int)product.GetCurrentTask().x)] / speed;
            //}

            AddTaskPointerObservations(index, nRings, product, lowerBound, observations, ref observationCount);

            if (product.task_pointer == nRings && nRings < 3)
            {
                AddEmptyTaskObservations(3 - nRings, observations);
            }

            h = Mathf.Max(h, lowerBound);
            AddFutureTaskObservations(product, productJob, lowerBound, nRings, observations);
        }
    }

    private void AddCompletedTaskObservations(int index, int nRings, Product product, List<float> observations, ref int observationCount)
    {
        for (int i = 0; i < productsTimes[index-1].Count; i++) 
        {
            observations.AddRange(new float[] { 1, NormalizeTimeValues(productsTimes[index-1][i])});
            if (i != 0)
            {
                observations.Add(0f);
            }
            if (i >= 1 && i <= 3 && i <= nRings)
            {
                float id = 0.5f;
                if ((int)productsCharacteristics[index - 1][1+i] == 4 || (int)productsCharacteristics[index - 1][1+i] == 5)
                {
                    id = 1f;
                }
                observations.AddRange(new float[] {
                    id,
                    (float)combProdRequired[(int)productsCharacteristics[index - 1][1 + i]]/3+0.1f,
                    NormalizeDistanceValue(stationDistances[(baseStationDictionary.First().Key, ringstationsTasks[(int)productsCharacteristics[index - 1][1 + i]])])
                });
                observationCount++;
            }
            observationCount += 2;
            if (product.product_ID == debbugProductID)
            {
                lower_bounds[i] = productsTimes[index-1][i];
            }
            h = Mathf.Max(h, productsTimes[index - 1][i]);

            if (i == nRings && nRings < 3)
            {
                AddEmptyTaskObservations(3 - nRings, observations);
            }
            else if (i == nRings + 1)
            {
                float id = 0.5f;
                if ((int)productsCharacteristics[product.product_ID - 1][2 + nRings] == 7)
                {
                    id = 1f;
                }
                observations.AddRange(new float[] {
                id
                 });
            }
        }
    }

    private float CalculateCurrentTaskTime( Product product, List<Vector3> productJob, float lowerBound, List<float> observations)
    {
        float currentTaskTime = 0;
        int agentID = -1;
        foreach (KeyValuePair<int, JSSPMultiAgent> agent in agents)
        {
            if (agent.Value.jobProduct == product.product_ID)
            {
                agentID = agent.Key;
            }
        }

        if (agentID!=-1 && agents[agentID].StartedGrabbing())
        {
            NavMesh.CalculatePath(product.transform.position, inputLocationDictionary[(int)productJob[product.task_pointer].x].position, NavMesh.AllAreas, path);
            for (int j = 0; j < path.corners.Length - 1; j++)
            {
                currentTaskTime += Vector3.Distance(path.corners[j], path.corners[j + 1]);
                //Debug.DrawLine(path.corners[j], path.corners[j + 1], Color.red, 0.5f, false);
            }
            lowerBound =lowerBound+ agents[agentID].GrabbingDuration() - (Time.time - agents[agentID].StartedGrabbingTime()) + lbDroppingTime  + (currentTaskTime / speed) + productJob[product.task_pointer].y;
            observations.Add(NormalizeTimeValues(lowerBound));
            observations.Add(NormalizeDistanceValue(currentTaskTime));
        }

        else if (agentID != -1 && agents[agentID].StartedDropping())
        {
            lowerBound = lowerBound + agents[agentID].DroppingDuration()-(Time.time - agents[agentID].StartedDroppingTime()) + productJob[product.task_pointer].y;
            observations.Add(NormalizeTimeValues(lowerBound));
            observations.Add(0);
        }
        else if (agentID != -1 && product.grabbed)
        {
            NavMesh.CalculatePath(agents[agentID].transform.position, inputLocationDictionary[(int)productJob[product.task_pointer].x].position, NavMesh.AllAreas, path);
            for (int j = 0; j < path.corners.Length - 1; j++)
            {
                //path.corners[j].y = 0;
                //path.corners[j+1].y = 0;
                currentTaskTime += Vector3.Distance(path.corners[j], path.corners[j + 1]);
                Debug.DrawLine(path.corners[j], path.corners[j + 1], Color.red, 0.5f, false);
            }
            lowerBound =  lowerBound +  lbDroppingTime + (currentTaskTime / speed) + productJob[product.task_pointer].y;
            observations.Add(NormalizeTimeValues(lowerBound));
            observations.Add(NormalizeDistanceValue(currentTaskTime));
        }
        else if(agentID != -1 && product.assigned)
        {
            float dist = 0;
            dist = stationDistances[((int)productJob[product.task_pointer - 1].x, (int)productJob[product.task_pointer].x)];
            lowerBound= lowerBound + lbDroppingTime +lbGrabingTime+ dist/speed + productJob[product.task_pointer].y ;
            observations.Add(NormalizeTimeValues(lowerBound));
            observations.Add(NormalizeDistanceValue(stationDistances[((int)productJob[product.task_pointer - 1].x, (int)productJob[product.task_pointer].x)]));
        }
        else
        {
            float dist = 0;
            dist = stationDistances[((int)productJob[product.task_pointer - 1].x, (int)productJob[product.task_pointer].x)];
            lowerBound = lowerBound + lbDroppingTime + lbGrabingTime + dist/speed + productJob[product.task_pointer].y;
            observations.Add(NormalizeTimeValues(lowerBound));
            observations.Add(NormalizeDistanceValue(stationDistances[((int)productJob[product.task_pointer - 1].x, (int)productJob[product.task_pointer].x)]));
        }
        return lowerBound;
    }

    private void AddTaskPointerObservations(int index, int nRings, Product product, float lowerBound, List<float> observations, ref int observationCount)
    {
        if (product.task_pointer >= 1 && product.task_pointer <= 3 && product.task_pointer <= nRings)
        {
            float id = 0.5f;
            if ((int)productsCharacteristics[index - 1][1 + product.task_pointer] == 4 || (int)productsCharacteristics[index - 1][1 + product.task_pointer] == 5)
            {
                id = 1f;
            }
            observations.AddRange(new float[] {
                id,
                (float)combProdRequired[(int)productsCharacteristics[index - 1][1 + product.task_pointer]]/3+0.1f,
                NormalizeDistanceValue(stationDistances[(baseStationDictionary.First().Key, ringstationsTasks[(int)productsCharacteristics[index - 1][1 + product.task_pointer]])])
            });
            observationCount++;
        }
        if (product.task_pointer == nRings + 1)
        {
            float id = 0.5f;
            if ((int)productsCharacteristics[product.product_ID - 1][2 + nRings] == 7)
            {
                id = 1f;
            }
            observations.AddRange(new float[] {
                id
            });
        }
        observationCount += 2;
        if (product.product_ID == debbugProductID)
        {
            lower_bounds[product.task_pointer] = lowerBound;
        }
    }

    private void AddEmptyTaskObservations(int count, List<float> observations)
    {
        for (int i = 0; i < count; i++)
        {
            observations.AddRange(new float[] {0 ,0, 0, 0, 0, 0 });
        }
    }

    private void AddFutureTaskObservations(Product product, List<Vector3> productJob, float lowerBound, int nRings, List<float> observations)
    {
        for (int k = product.task_pointer + 1; k < productJob.Count ; k++)
        {
            float dist = 0;
            dist = stationDistances[((int)productJob[k-1].x, (int)productJob[k].x)];
            
            lowerBound = lowerBound + lbGrabingTime + lbDroppingTime  + (dist / speed) + productJob[k].y;
            observations.Add(0f);
            if (k >= 1 && k <= 3 && k <= nRings)
            {
                //lowerBound += (float)combProdRequired[(int)productsCharacteristics[product.product_ID - 1][1 + k]] *
                //    stationDistances[((int)baseStationDictionary.First().Key, (int)product.GetJob()[k].x)]/speed; 
                observations.Add(NormalizeTimeValues(lowerBound));
                observations.Add(NormalizeDistanceValue(stationDistances[((int)productJob[k - 1].x, (int)productJob[k].x)]));
                float id = 0.5f;
                if ((int)productsCharacteristics[product.product_ID - 1][1 + k] == 4 || (int)productsCharacteristics[product.product_ID - 1][1 + k] == 5)
                {
                    id = 1f;
                }
                observations.AddRange(new float[] {
                    id,
                    (float)combProdRequired[(int)productsCharacteristics[product.product_ID - 1][1 + k]]/3+0.1f,
                    NormalizeDistanceValue(stationDistances[(baseStationDictionary.First().Key, ringstationsTasks[(int)productsCharacteristics[product.product_ID - 1][1 + k]])])
                });
            }
            else
            {
                observations.Add(NormalizeTimeValues(lowerBound));
                observations.Add(NormalizeDistanceValue(stationDistances[((int)productJob[k - 1].x, (int)productJob[k].x)]));
            }
            h = Mathf.Max(h, lowerBound);
            if (k == nRings && nRings < 3)
            {
                AddEmptyTaskObservations(3 - nRings, observations);
            }
            if (k == nRings + 1)
            {
                float id = 0.5f;
                if ((int)productsCharacteristics[product.product_ID - 1][2 + nRings] == 7 )
                {
                    id = 1f;
                }
                observations.AddRange(new float[] {
                    id
                });
            }
        }
    }

    private void CalculateAndLogRewards()
    {
        reward = last_h - h;
        //if (reward > 0)
        //{
        //    int k=0;
        //}

        cummulativeReward += reward;
        if (verbose)
        {
            Debug.Log("reward:" + reward);
            Debug.Log("cummulativeReward:" + cummulativeReward);
        }
        //if (cummulativeReward < -800 && -830<cummulativeReward)
        //{
        //    DLogs();
        //}
    }



    public void OnApplicationQuit()
    {
        if (printMakespan)
        {
            makespanWriter.Flush();
            makespanWriter.Close();
        }
    }

    public Dictionary<int, Product> GetProducts()
    {
        return productDictionary;
    }
    
    public Dictionary<int,Workstation> GetStations()
    {
        return workStationDictionary;
    }
    
    public Dictionary<int, BaseStation> GetBaseStations()
    {
        return baseStationDictionary;
    }
    
    public Dictionary<int, RingStation> GetRingStations()
    {
        return ringStationDictionary;
    }
    
    public float GetEpisodeStart()
    {
        return episode_start;
    }

    public Dictionary<int, CapStation> GetCapStations()
    {
        return capStationDictionary;
    }
    
    public Dictionary<int, DeliveryStation> GetDeliveryStations()
    {
        return deliveryStationDictionary;
    }
    
    public Dictionary<int, int> GetRequiredCombDict()
    {
        return combProdRequired;
    }
    
    public bool IsInDeadlock()
    {
        return deadlock;
    }
   
    public void InDeadlock()
    {
        deadlock = true;
    }
    
    public void DeadlockResolved()
    {
        deadlock = false;
    }
    
    public List<Color> GetCollorList()
    {
        return color_list;
    }
    
    public Color GetColor(int num)
    {
        return color_list[num];
    }
    
    public float GetStartTime()
    {
        return startTime;
    }
    
    public Dictionary<int,int> GetCombProdRequired()
    {
        return combProdRequired;
    }

    public Dictionary<(int, int), float> GetStationDistances()
    {
        return stationDistances;
    }
   
    public void AddProductTime(int product_ID, float t)
    {
        productsTimes[product_ID - 1].Add(t);
    }
    public float getEpisodeStart()
    {
        return episode_start;
    }
    public List<float>[] GetProductsTimes()
    {
        return productsTimes; 
    }
    public int GetNProducts()
    {
        return n_products;
    }
}
