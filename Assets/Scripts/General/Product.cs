using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

public class Product : MonoBehaviour
{
    //static variable
    public static int ID = 1;

    // Colors
    private List<Color> color_list;
    private List<Color> bs_colors;
    private List<Color> rs_colors;
    public int product_ID = 0;
    public bool randomJob;
    private int jobSeed=0;
    private int machineSeed;

    


    [Tooltip("Ordered list of tasks to complete job. x: main machine associated with task, y: processing time, z:task. Add delivery station at the end with a negative ID at x")]
    public List<Vector3> job;

    [HideInInspector]
    public int task_pointer = 0;
    private Material product_material;
    public bool processing= false;
    public bool finished = false;

    [HideInInspector]
    public float process_start = 0;
    public float starting_time = 0;


    // Times of Completion
    public List<float> times;

    // Workstations
    private Collider used_w;
    private Workstation curr_workstation;
    private Vector3 outputPosition;
    private Dictionary<int, BaseStation> basestations;
    private Dictionary<int, Vector3> basestations_output;
    private Dictionary<int, Vector3> basestations_process;
    private Dictionary<int, int> ringstationsTasks;
    private Dictionary<int, int> capstationsTask;
    private List<float[]> productsCharacteristics;
    public int bStationInstID = 0;
    public int dStationInstID = 0;
    public JSSPMultiAgent curr_agent;
    private MultiAgentController controller;


    [HideInInspector]
    public int workstationPlacementID = 0;


    // Initial Parameters
    [HideInInspector]
    public Transform og_parent;
    [HideInInspector]
    public Vector3 og_position;
    private float episodeStart;

    [HideInInspector]
    public bool grabbed= false;

    [HideInInspector]
    public bool assigned = false;
    public bool blocked = false;
    public bool processed = true;

    [Header("Debug")]
    private int Nw;
    private float ogTimeScale;


    private void Awake()
    {
        // Initialize
        processing = false;
        outputPosition = new Vector3();
        og_parent = transform.parent;
        og_position = transform.position;
        episodeStart = Time.time;
        basestations = new Dictionary<int, BaseStation>();
        basestations_output = new Dictionary<int, Vector3>();
        basestations_process = new Dictionary<int, Vector3>();
        finished = false;
        product_ID = Product.ID;
        Product.ID++;

        
        //blocked = true;

        CollectStations();
        //Transform temp = og_parent.parent;
        /*
        foreach (BaseStation station in og_parent.GetComponentsInChildren<BaseStation>())
        {
            basestations.Add(station.ID, station);
            basestations_output.Add(station.ID, station.GetOutputLocation().Item1);
        }
        */
        // Get Coloring List and Change Color
        controller = transform.parent.GetComponentInParent<MultiAgentController>();
        FASinfo info = transform.parent.GetComponentInParent<FASinfo>();
        color_list = info.color_list;
        bs_colors = info.bs_colors;
        rs_colors = info.rs_colors;
        Nw = info.Nw;
        jobSeed = info.jobSeed;
        //if (jobSeed > 0)
        //{
        //    UnityEngine.Random.InitState(jobSeed);
        //}
        FASController cont = transform.parent.GetComponentInParent<FASController>();
        

        ringstationsTasks = cont.GetRingstationsTasks();
        capstationsTask = cont.GetCapstationsTask();
        productsCharacteristics = cont.GetProductsCharacteristics();
        //starting_time = UnityEngine.Random.Range(0f, 10f);
        starting_time = productsCharacteristics[product_ID-1][0];
        bStationInstID = cont.bStationInstID;
        dStationInstID = cont.dStationInstID;
        if (productsCharacteristics.Count+1 == Product.ID)
        {
            Product.ID = 1;
        }
        //randomJob = info.randomJob;
        //jobSeed = info.jobSeed;
        //machineSeed = info.machineSeed;

        // Generate job
        //UnityEngine.Random.InitState(jobSeed);
        job = new List<Vector3>();
        GenerateJob2();

        // if location in

        //origin = info.Origin.position;
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        product_material = meshRenderer.material;
        ChangeColor(product_material, color_list[(int)job[task_pointer][0]]);

        ogTimeScale = Time.timeScale;

        
        BaseStation basestation = og_parent.GetComponent<BaseStation>();
        if (basestation)
        {
            basestation.ReceiveProduct(transform.GetComponent<Product>());
        }

    }

    public void CollectStations()
    {
        Transform temp = og_parent.parent;
        for (int i = 0; i < temp.childCount; i++)
        {
            Transform child = temp.GetChild(i);
            if (child.CompareTag("basestation"))
            {
                BaseStation station = child.GetComponent<BaseStation>();
                basestations.Add(station.ID, station);
                basestations_output.Add(station.ID, station.GetOutputLocation().Item1);
                basestations_process.Add(station.ID, station.GetProcessLocation());
            }
            

        }

    }
    /*
    public void checkInMachine()
    {
        Transform parent = transform.parent;
        if (!processing && parent.CompareTag("basestation"))
        {
            BaseStation basestation = parent.GetComponent<BaseStation>();
            assigned = true;
            if(basestation.ProductCount() == 0)
            {
                workstation.ReceiveProduct(transform.GetComponent<Product>());
                PerformTask(Time.time, basestation);

            }
            else
            {
                basestation.ReceiveProduct(transform.GetComponent<Product>());
                blocked = true;
            }
        }

    }
    */

    /*
    private void GenerateJob()
    {
        
        for(int i = 0; i < Nw; i++)
        {
            for (int j = 0; j < product_ID*Nw; j++) { float _ = UnityEngine.Random.Range(1, 10); }
            float t = UnityEngine.Random.Range(1f, 10f);
            job.Add(new Vector2(i + 1f, t));
        }

        UnityEngine.Random.InitState(machineSeed);
        List<Vector2> temp_list = new List<Vector2>(job);
        for(int i = 0; i< temp_list.Count; i++)
        {
            for (int j = 0; j < product_ID * Nw; j++) { float _ = UnityEngine.Random.Range(1, 10); }
            int temp = UnityEngine.Random.Range(0, Nw);
            job[i] = temp_list[temp];
            job[temp] = temp_list[i];
            temp_list = new List<Vector2> (job);
        }


        // Add delivery stations
        job.Add(new Vector2(-1, 0));
    }
    */
    // Number for the rcll case : BS: 4-7 , RS: 20-25, CS:20-25, DS:6-8
    //Generalise the number of machines
    private void GenerateJob2()
    {
        //Basestation
        int found_id = -1;
        float[] currProdCharac = productsCharacteristics[product_ID - 1];
        foreach (KeyValuePair<int, Vector3> kvp in basestations_process)
        {

            //Vector3 process_location;
            //process_location = og_parent.GetComponent<Workstation>().GetProcessLocation();
            if (kvp.Value == transform.position)
            {
                found_id = kvp.Key;
                break;
            }
        }
        if (found_id > -1)
        {
            transform.parent = basestations[found_id].transform;
            job.Add(new Vector3(found_id, UnityEngine.Random.Range(7f, 12f)+starting_time, found_id)); //7 12 or 5 6

        }
        else
        {
            int temp1 = UnityEngine.Random.Range(1, 3);
            job.Add(new Vector3(temp1, UnityEngine.Random.Range(7f, 12f)+starting_time, temp1));
        }
        
        int num_ring_op = (int)currProdCharac[1];

        //Ringstations jobs
        for (int i = 0; i < num_ring_op; i++)
        {
            int currTask = (int)currProdCharac[2 + i];
            //for (int j = 0; j < product_ID * Nw; j++) { float _ = UnityEngine.Random.Range(1, 10); }
            job.Add(new Vector3(ringstationsTasks[currTask], UnityEngine.Random.Range(20f, 25f), currTask)); //20 40 or 26 30

        }

        //Capstationjob
        int curr_cap_task_ID = (int)currProdCharac[2 + num_ring_op];
        float temp = capstationsTask[curr_cap_task_ID];
        job.Add(new Vector3(capstationsTask[curr_cap_task_ID], UnityEngine.Random.Range(20f, 25f), curr_cap_task_ID)); // 7 12 or 5 6  15f, 25f

        //deliverystation
        job.Add(new Vector3(dStationInstID, UnityEngine.Random.Range(5f, 10f), 8)); //10 20 or 14 16
        int k = 0;
    }


    private void GenerateJob()
    {
        //Basestation
        int found_id = -1;
        foreach (KeyValuePair<int, Vector3> kvp in basestations_process)
        {
            //Vector3 process_location;
            //process_location = og_parent.GetComponent<Workstation>().GetProcessLocation();
            if (kvp.Value == transform.position)
            {
                found_id = kvp.Key;
                break;
            }
        }
        if (found_id>-1)
        {
            transform.parent = basestations[found_id].transform;
            job.Add(new Vector3(found_id, UnityEngine.Random.Range(1f, 2f), found_id));
            
        }
        else
        {
            int temp1 = UnityEngine.Random.Range(1, 3);
            job.Add(new Vector3(temp1, UnityEngine.Random.Range(1f, 2f), temp1));
        }
        int num_ring_op=  UnityEngine.Random.Range(0, Nw+1);
        //Ringstations jobs
        for (int i = 0; i < num_ring_op; i++)
        {
            //for (int j = 0; j < product_ID * Nw; j++) { float _ = UnityEngine.Random.Range(1, 10); }
            int t = UnityEngine.Random.Range(3, 7);
            if (t==3 || t == 4)
            {
                job.Add(new Vector3(2, UnityEngine.Random.Range(6f, 12f), t)); //40 60
            }
            else
            {
                job.Add(new Vector3(3, UnityEngine.Random.Range(6f, 12f), t));
            }
            
        }
        //UnityEngine.Random.InitState(machineSeed);
        List<Vector3> temp_list = new List<Vector3>(job);
        for (int i = 1; i < (temp_list.Count-1); i++)
        {
            //for (int j = 0; j < product_ID * Nw; j++) { float _ = UnityEngine.Random.Range(1, 10); }
            int temp = UnityEngine.Random.Range(1, 1+Nw);
            job[i] = temp_list[temp];
            job[temp] = temp_list[i];
            temp_list = new List<Vector3>(job);
        }
        //Capstationjob
        int cap_ID = UnityEngine.Random.Range(4, 6);
        job.Add(new Vector3(cap_ID, UnityEngine.Random.Range(5f, 7f), 7)); // 15 25

        //deliverystation
        job.Add(new Vector3(6, UnityEngine.Random.Range(6f, 12f), 8)); //
    }

    private void Start()
    {
        for (int i = 0; i < job.Count; i++)
        {
            times.Add(-1f);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Task Completion
        if (processing) { 
            ContinueTask(Time.time); 
        }
    }

    private void ChangeColor(Material material, Color color) {
        material.SetColor("_Color", color);
    }

    public void PerformTask(float init_time, Transform transform_parent)
    {
        float time = Time.time;
        //coll.gameObject.SetActive(false); ??
        
        Workstation station = transform_parent.GetComponent<Workstation>();
        used_w = station.input_collider;
        process_start = time;
        if (time < init_time + job[task_pointer].y)
        {// In case tp > 0
            processing = true;
            //ChangeColor(product_material, color_list[0]); à voir plus tard
            used_w.gameObject.SetActive(true);
            SetParent(station.transform);
            (outputPosition, workstationPlacementID) = station.GetOutputLocation();
            SetPosition(curr_workstation.processing_location.position);
        }
        else
        {// In case of tp = 0
            float t = Time.time - episodeStart;
            SetParent(station.transform);
            process_start = 0;
            (outputPosition, workstationPlacementID) = station.GetOutputLocation();
            //SetPosition(outputPosition);
            processing = false;
            times[task_pointer] = t;
            controller.AddProductTime(product_ID, t);
            if (curr_workstation.CompareTag("deliverystation"))
            {
                CompleteJob();
                Destroy(transform.gameObject);
            }
            else
            {
                //Debug.Log("ContinueTask Ran");
                if (task_pointer <= job.Count - 1) { 
                    task_pointer++; 
                }
                used_w.gameObject.SetActive(true);
                /*
                if ((int)job[task_pointer][0] > 0)
                { ChangeColor(product_material, color_list[(int)job[task_pointer].x]); }
                else
                { ChangeColor(product_material, color_list[color_list.Count - 1]); }
                */
                grabbed = false;
                assigned = false;
                /*
                if (!curr_workstation.waiting)
                {
                    UnBlock();
                }
                */
            }
            if (!curr_workstation.CompareTag("basestation"))
            {
                curr_workstation.CurrProductFinish();
            }
            else
            {
                BaseStation b = curr_workstation as BaseStation;
                b.Finish(product_ID);
                UnBlock();
            }
        }
    }
    /*
    public void PerformTask(float init_time, Collider coll)
    {
        // Task Initialization
        float time = Time.time;
        coll.gameObject.SetActive(false);
        used_w = coll;
        workstation = coll.transform.GetComponentInParent<Workstation>();
        process_start  = time;
        
        if (time<init_time + job[task_pointer].y) 
        {// In case tp > 0
            processing = true;
            ChangeColor(product_material, color_list[0]);
            SetParent(coll.transform.parent);
            (outputPosition, workstationPlacementID) = workstation.GetOutputLocation();
            SetPosition(workstation.processing_location.position);
            grabbed = true;
        }
        else
        {// In case of tp = 0
            if (workstation.CompareTag("deliverystation"))
            {
                CompleteJob();
            }
            processing = false;
            float t = Time.time - episodeStart;
            //Debug.Log("PerformedTask Ran");
            times[task_pointer] = t;
            if (task_pointer < job.Count-1) { task_pointer++; }
            /*
            if ((int)job[task_pointer][0] > 0)
            { ChangeColor(product_material, color_list[(int)job[task_pointer].x]); }
            else
            { ChangeColor(product_material, color_list[color_list.Count - 1]); }
            SetParent(coll.transform.parent);
            //Debug.Log("Product ID: " + product_ID + " Finished Task # " + task_pointer + " At: " + t);
            (outputPosition, workstationPlacementID) = workstation.GetOutputLocation();
            SetPosition(outputPosition);
            used_w.gameObject.SetActive(true);
            grabbed = false;
            assigned = false;
        }
    }

    public void PerformTask(float init_time, BaseStation baseStation)
    {
        float time = Time.time;
        Collider coll = baseStation.transform.GetComponentInChildren<Collider>(); //???
        coll.gameObject.SetActive(false);
        used_w = coll;
        process_start = time;

        if (time < init_time + job[task_pointer].y)
        {// In case tp > 0
            processing = true;
            ChangeColor(product_material, color_list[0]);
            SetParent(coll.transform.parent);
            (outputPosition, workstationPlacementID) = baseStation.GetOutputLocation();
            SetPosition(baseStation.processing_location.position);
            grabbed = true;
        }
        else
        {// In case of tp = 0
            processing = false;
            float t = Time.time - episodeStart;
            //Debug.Log("PerformedTask Ran");
            times[task_pointer] = t;
            if (task_pointer < job.Count - 1) { task_pointer++; }
            if ((int)job[task_pointer][0] > 0)
            { ChangeColor(product_material, color_list[(int)job[task_pointer].x]); }
            else
            { ChangeColor(product_material, color_list[color_list.Count - 1]); }
            SetParent(coll.transform.parent);

            //Debug.Log("Product ID: " + product_ID + " Finished Task # " + task_pointer + " At: " + t);
            (outputPosition, workstationPlacementID) = baseStation.GetOutputLocation();
            SetPosition(outputPosition);
            used_w.gameObject.SetActive(true);
            grabbed = false;
            assigned = false;
        }
    }
    */
    private void ContinueTask(float time)
    {
        //Only runs when the task is completed
        if (time > process_start + job[task_pointer].y)
        {
            float t = Time.time - episodeStart;

            process_start = 0;
            //SetPosition(outputPosition);
            processing = false;
            times[task_pointer] = t;
            controller.AddProductTime(product_ID, t);
            if (curr_workstation.CompareTag("deliverystation"))
            {
                CompleteJob();
                Destroy(transform.gameObject);
            }
            else
            {
                //Debug.Log("ContinueTask Ran");
                if (task_pointer < job.Count - 1) { task_pointer++; }
                used_w.gameObject.SetActive(true);
                /*
                if ((int)job[task_pointer][0] > 0)
                { ChangeColor(product_material, color_list[(int)job[task_pointer].x]); }
                else
                { ChangeColor(product_material, color_list[color_list.Count - 1]); }
                */
                grabbed = false;
                assigned = false;
                /*
                if (!curr_workstation.waiting)
                {
                    UnBlock();
                }
                */
            }
            if (!curr_workstation.CompareTag("basestation"))
            {
                curr_workstation.CurrProductFinish();
            }
            else
            {
                BaseStation b = curr_workstation as BaseStation;
                b.Finish(product_ID);
                UnBlock();
            }
            //Debug.Log("Product ID: " + product_ID + " Finished Task # " + task_pointer + " At: " + t);
            //Debug.Log("To Go: " + GetLowerMakespan(2f));
        }
    }
    public void StartProcessing()
    {
        
    }


    public void CompleteJob()
    {
        float t = Time.time - episodeStart;
        //Debug.Log("CompletedJob Ran");
        times[task_pointer] = t;
        //controller.AddProductTime(product_ID, t);
        //Debug.Log("Product ID: " + product_ID + " Finished Completely At: " + t);
        transform.gameObject.SetActive(false);
        grabbed = true;
        finished = true;
        assigned = true;
        blocked = true;
        DeliveryStation d_station = transform.GetComponentInParent<DeliveryStation>();
        if (d_station)
        {
            d_station.UnAssign();
            d_station.UnBlock();
            d_station.ProductFinished();
        }
        //Destroy(transform.gameObject);
    }

    
    
    public void SetParent(Transform parent)
    {
        transform.parent = parent;
        Workstation station = parent.GetComponent<Workstation>();
        JSSPMultiAgent agent = parent.GetComponent<JSSPMultiAgent>();
        if (station)
        {
            curr_workstation = station;
            curr_agent = null;
        }
        else if (agent)
        {
            curr_agent = agent;
            curr_workstation = null;
        }
    }

    public void SetinTable(Transform table)
    {
        SetParent(table);
        SetPosition(table.gameObject.GetComponent<TemporalStation>().load.position);
    }

    public void EpisodeReset()
    {
        task_pointer = 0;
        processing = false;
        times = new List<float>();
        for (int i = 0; i < job.Count; i++)
        {
            times.Add(-1f);
        }
        outputPosition = new Vector3();
        //Debug.Log(color_list[(int)job[task_pointer][0]]);
        ChangeColor(product_material, color_list[(int)job[task_pointer][0]]);
        SetParent(og_parent);
        SetPosition(og_position);
        episodeStart = Time.time;
        used_w = new Collider();
        grabbed = false;
        transform.gameObject.SetActive(true);
        finished = false;
        assigned = false;
        blocked = false;
    }

    public Vector3 GetDestination(int grabbed_ID, Dictionary<int, Transform> workstationLocations)
    {
        Vector3 destination = new Vector3(0,0,0);
        if (!grabbed)
        {
            destination =  transform.position;

        }
        else if(grabbed_ID == product_ID)
        {
            int temp_ID = (int)GetCurrentTask()[0]; // get ID of next task
            Transform temp_w = workstationLocations[temp_ID];
            destination = temp_w.position;
        }
        return destination;
    }

    public float RemainingTime()
    {
        if (blocked)
        {
            return job[task_pointer].y - (Time.time - process_start);
        }
        return 0;
    }

    public void Process()
    {
        processing = true;
    }

    public void StopProcessing()
    {
        processing = false;
    }

    public void Block()
    {
        blocked = true;
    }

    public void UnBlock()
    {
        blocked = false;
    }
    
    public bool IsBlocked()
    {
        return blocked;
    }

    public List<Vector3> GetJob()
    {
        return job;
    }

    public void SetStation(Workstation station)
    {
        curr_workstation = station;
    }
    public void LeaveStation()
    {
        curr_workstation = null;
    }
    public Workstation GetCurrStation()
    {
        return curr_workstation;
    }

    public List<float> GetTimes()
    {
        return times;
    }

    public Vector3 GetCurrentTask()
    {
        return job[task_pointer];
    }

    public Vector3 GetPreviousTask()
    {
        if (task_pointer > 0)
        {
            return job[task_pointer - 1];
        }
        return new Vector3(-1f, -1f, -1f);
    }

    public Vector3 GetNextTask()
    {
        if(task_pointer < job.Count - 1)
        {
            return job[task_pointer + 1];
        }
        return new Vector3(-1f,-1f,-1f);
    }

    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
    }
    
    public bool JobInSameMachine()
    {
        if (task_pointer>0 && job[task_pointer].x == job[task_pointer-1].x)
        {
            return true;
        }
        return false;
    }
    public bool LastMachine()
    {
        if(job.Count-1 == task_pointer)
        {
            return true;
        }
        return false;
    }
    
    public void Desactivate()
    {
        transform.gameObject.SetActive(false);
    }
    public bool IsGrabbed()
    {
        return grabbed;
    }
    public void Reset()
    {
        Product.ID = 1;
    }
    public Workstation GetCurrWorkstation()
    {
        return curr_workstation;
    }
}
