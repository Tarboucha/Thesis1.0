using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine.AI;


public class JSSPMultiAgent : Agent
{
    [Tooltip("NavMesh Agent")]
    public NavMeshAgent agent;

    //new private Rigidbody rigidbody;

    [Tooltip("Location of Carried products.")]
    public Transform load;

    [Tooltip("Main FAS GameObject Controller")]
    public MultiAgentController controller;

    [Tooltip("Main FAS GameObject Configuration Varibles")]
    public FASinfo fas_info;

    public int ID;
    //public int working = false;

    private float startTime = 0;

    [Header("Heuristics")]
    public bool inference = false;
    public bool manual=false;
    public bool SPT = false;
    public bool LPT = false;

    //Product vars
    public int last_action;
    private Product product;
    private CombProduct combProduct;
    public bool carrying_p;
    public bool carrying_cp;
    private int task;
    private int mWaiting=-1;
    private bool working=false;

    //private float lowerGrabingTime = 6f;
    //private float lowerDropingTime = 6f;

    //Dictionaries
    Workstation workstation;
    //Episode linked
    private Dictionary<int, Transform> inputLocationDictionary;
    private Dictionary<int, Transform> outputLocationDictionary;
    private Dictionary<Collider, Workstation> workstationColliderDictionary;
    private Dictionary<int, Product> productDictionary;
    private Dictionary<int, Workstation> stations;
    private Dictionary<int, BaseStation> basestations;
    public Dictionary<int, RingStation> ringStationDictionary;
    public Dictionary<int, CapStation> capStationDictionary;
    public Dictionary<int, DeliveryStation> deliveryStationDictionary;
    private Dictionary<(int, int), float> stationDistances;
    private Dictionary<int, int> basestationIDs;
    private int baseStationID;

    private Dictionary<int, JSSPMultiAgent> agentDictionary;
    private Dictionary<int, int> combProdRequired;
    private int n_products = 11;
    public bool decisionRequested = false;
    public int stepAction = -1;
    public int n_zeros = 0;
    public int zeros_ite = 0;

    private float grabbingTimeUB;
    private float grabbingTimeLB;
    private float droppingTimeUB;
    private float droppingTimeLB;
    private float speed;

    //Initial Position & Rotation
    [HideInInspector]
    public Vector3 init_pos;
    [HideInInspector]
    public Quaternion init_rot;

    //Grabbing
    protected bool startedGrabbing = false;
    protected float timeGrabbing;
    protected float startingTimeGrabbing;

    // Product Grabbing
    [Header("Product")]
    public int jobProduct = -1;
    public int grabbedProductID;

    //Droping
    protected bool startedDropping = false;
    protected float timeDropping;
    protected float startingTimeDropping;

    //CombProduct Grabbing
    //public int jobComb=-1;
    public int grabbedCombID;

    // Distances vars
    NavMeshPath path;
    private bool samePos=false;
    private bool allReady = true;

    // Debug
    [Header("Debug")]
    public bool decided= false;
    public Collider debug_collider;
    public GameObject Dot;

    //NavMesh Train Fix
    [Header("NavMesh Fix")]
    private Vector3 destination;

    public override void Initialize()
    {
        //Initialize
        carrying_p = false;
        carrying_cp = false;
        //rigidbody = GetComponent<Rigidbody>();
        inputLocationDictionary = new Dictionary<int, Transform>();
        outputLocationDictionary = new Dictionary<int, Transform>();
        workstationColliderDictionary = new Dictionary<Collider, Workstation>();
        stations =  new Dictionary<int, Workstation>();
        basestations = new Dictionary<int, BaseStation>();
        productDictionary = new Dictionary<int, Product>();
        stationDistances = new Dictionary<(int, int), float>();
        agentDictionary = new Dictionary<int, JSSPMultiAgent>();
        basestationIDs = new Dictionary<int, int>();
        combProdRequired = new Dictionary<int, int>();
        jobProduct = -1;
        //jobComb = -1;
        fas_info = GetComponentInParent<FASinfo>();
        n_products = fas_info.n_products;
        grabbingTimeUB = fas_info.grabbingTimeUB;
        grabbingTimeLB = fas_info.grabbingTimeLB;
        droppingTimeUB = fas_info.droppingTimeUB;
        droppingTimeLB = fas_info.droppingTimeLB;

        agent.updatePosition = false;
        CheckforProduct();
        path = new NavMeshPath();
        startTime = controller.GetStartTime();
        

        int mWaiting = 0;
        //Get locations
        init_pos = transform.position;
        init_rot = transform.rotation;
        last_action = 0;
        destination = init_pos;
        agent.updatePosition = false;
        speed= fas_info.agent_speed;
        agent.speed = speed;

        //agent.isStopped = true;
        //UnityEngine.Random.InitState(20);
        //GetLocations(transform.parent);
    }
    void Start()
    {
        GetLocations(transform.parent,false);

        combProdRequired = controller.GetCombProdRequired();
       
    }
    public void Reset()
    {
        //Initialize
        carrying_p = false;
        carrying_cp = false;
        //rigidbody = GetComponent<Rigidbody>();
        inputLocationDictionary = new Dictionary<int, Transform>();
        outputLocationDictionary = new Dictionary<int, Transform>();
        workstationColliderDictionary = new Dictionary<Collider, Workstation>();
        stations = new Dictionary<int, Workstation>();
        basestations = new Dictionary<int, BaseStation>();
        productDictionary = new Dictionary<int, Product>();
        stationDistances = new Dictionary<(int, int), float>();
        //agentDictionary = new Dictionary<int, JSSPMultiAgent>();
        basestationIDs = new Dictionary<int, int>();
        combProdRequired = new Dictionary<int, int>();
        jobProduct = -1;
        //jobComb = -1;
        CheckforProduct();
        path = new NavMeshPath();
        startTime = controller.GetStartTime();
        

        int mWaiting = 0;
        //Get locations
        //init_pos = transform.position;
        //init_rot = transform.rotation;
        last_action = 0;
        destination = init_pos;
        agent.updatePosition = false;
        //agent.isStopped = true;
        //UnityEngine.Random.InitState(20);
        //GetLocations(transform.parent);
        GetLocations(transform.parent, true);
        combProdRequired = controller.GetCombProdRequired();
        n_products = controller.GetNProducts();
    }

    private void Update()
    {
        NavMeshHit hit;
        float maxAgentTravelDistance = Time.deltaTime * agent.speed;
        //Debug.Log(maxAgentTravelDistance);
        productDictionary = controller.GetProducts();

        //if (agent.destination != null)
        //{
        //    agent.SetDestination(agent.destination);
        //}

        //if (!decided && (carrying_cp || carrying_p))
        //{
        //    int k = 0;
        //}
        if (!agent.SamplePathPosition(NavMesh.AllAreas, maxAgentTravelDistance, out hit))
        {
            Vector3 new_pos = new Vector3();
            new_pos = hit.position;
            //Vector3 test = new_pos.normalized;
            new_pos.y = init_pos.y;
            //if (new_pos.x == transform.position.x && new_pos.y == transform.position.y) 33.53, 1.8 ; 33.49 ,1.18 ; 32.85,5.45   and 33.06,4.99 "(33.51, 0.02, 1.28)" "(33.51, 0.02, 1.28)"
            //{
            //    samePos = true;
            //}
            //else
            //{
            //    samePos = false;
            //}
            transform.position = new_pos;
            agent.nextPosition = transform.position;
        }
        else
        {
            agent.SamplePathPosition(NavMesh.AllAreas, agent.remainingDistance, out hit);
            Vector3 new_pos = new Vector3();
            new_pos = hit.position;
            new_pos.y = init_pos.y;
            //if (new_pos == transform.position)
            //{
            //    samePos = true;
            //}
            //else
            //{
            //    samePos = false;
            //}
            transform.position = new_pos;
            agent.nextPosition = transform.position;
        }

        //if (agent.hasPath)
        //{
        //    // Move the agent forward automatically based on its speed and NavMesh path
        //    agent.Move(agent.desiredVelocity * Time.deltaTime);

        //    // Optionally, ensure the agent stays on its path
        //    if (agent.remainingDistance <= maxAgentTravelDistance)
        //    {
        //        // The agent is close to its destination, ensure it reaches it
        //        agent.SetDestination(agent.destination);
        //    }

        //    //// Check if the agent's position has changed
        //    //if (Vector3.Distance(transform.position, agent.nextPosition) < 0.001f)
        //    //{
        //    //    samePos = true;
        //    //}
        //    //else
        //    //{
        //    //    samePos = false;
        //    //}
        //}


        CheckforProduct();
        //Non-changeable task
        if (carrying_p)
        {
            if (grabbedProductID > 0)
            {
                //if (actionBuffers.DiscreteActions[0] != job) { DropProduct(); }
                var prod = productDictionary[grabbedProductID];
                destination = prod.GetDestination(grabbedProductID, inputLocationDictionary);
                destination.y = init_pos.y;
                agent.destination = destination;
            }
        }
        if (carrying_cp)
        {
            if (grabbedCombID > 0)
            {
                //if (actionBuffers.DiscreteActions[0] != job) { DropProduct(); }
                Transform temp_w = inputLocationDictionary[mWaiting];
                destination = temp_w.position;
                destination.y = init_pos.y;
                agent.destination = destination;
            }
        }

        bool otherD = false;
        foreach (KeyValuePair<int, JSSPMultiAgent> ag in controller.agents)
        {
            if (ag.Key != ID && ag.Value.decisionRequested)
            {
                otherD = true;
            }
        }

        if (!otherD && !carrying_p && !carrying_cp && !decided && mWaiting<=0)
        {
            if (jobProduct > -1)
            {
                Product prod = productDictionary[jobProduct];
                prod.assigned = false;
                Workstation station = stations[(int)prod.GetCurrentTask()[0]];
                station.inFree = true;
                stations[(int)prod.GetPreviousTask()[0]].outFree = true;
                //station.UnAssign();
            }
            if (mWaiting > 0)
            {
                stations[mWaiting].WillNotReceive(1);
            }

            RequestDecision();
            if (decided)
            {

                decisionRequested = true;
            }
            controller.IncreaseStep();
        }
        ResetDecisions();

        if (Dot) { Instantiate(Dot, transform.position, Quaternion.identity); }

    }

    public void LateUpdate()
    {
        stepAction = -1;
        decisionRequested = false;
    }

    public void CheckforProduct()
    {
        product = transform.GetComponentInChildren<Product>();
        combProduct = transform.GetComponentInChildren<CombProduct>();
        if (product)
        {
            grabbedProductID = product.product_ID;
            carrying_p = true;
            carrying_cp = false;
        }
        else if (combProduct)
        {
            grabbedCombID = combProduct.combProduct_ID;
            carrying_p = false;
            carrying_cp = true;
        }
        else
        {
            grabbedProductID = -1;
            grabbedCombID = -1;
            carrying_p = false;
            carrying_cp = false;
        }
    }


    public void FixedUpdate()
    {
        



    }

    public float CalculateTimeForTask(Vector3 task)
    {
        float currTime = task.y;
        if (combProdRequired.ContainsKey((int)task.z))
        {
            int n = combProdRequired[(int)task.z];
            float transportTime = controller.GetStationDistances()[(baseStationID, (int)task.x)];
            currTime += n * transportTime;
        }
        return currTime;
    }

    /*
    public float CalculateDistanceTime(int workStationID)
    {
        float currentTaskTime = 0f;
        NavMesh.CalculatePath(transform.position, inputLocationDictionary[workStationID].position, NavMesh.AllAreas, path);

        for (int j = 0; j < path.corners.Length - 1; j++)
        {
            currentTaskTime += Vector3.Distance(path.corners[j], path.corners[j + 1]) / agent.speed;

        }
        return currentTaskTime;
    }
    */
    public Dictionary<(int, int), float> CalculateDistances()
    {
        Dictionary<(int, int), float> distances = new Dictionary<(int, int), float>();
        for (int i = 1; i< outputLocationDictionary.Count+1; i++)
        {
            for( int j = 1; j< inputLocationDictionary.Count+1; j++)
            {
              
                float distance = 0;
                NavMesh.CalculatePath(outputLocationDictionary[i].position, inputLocationDictionary[j].position, NavMesh.AllAreas, path);
                for (int k = 0; k < path.corners.Length - 1; k++)
                {
                    distance += Vector3.Distance(path.corners[k], path.corners[k + 1]);
                    if (i == 1 && j==2)
                    {
                        Debug.DrawLine(path.corners[k], path.corners[k + 1], Color.red, 10f, false);
                    }
                }
                distances.Add((i, j), distance);
                //Debug.Log(distance);
            }
            /*
            for (int j = 1; j < deliveryLocationDictionary.Count + 1; j++)
            {
                float distance = 0;
                NavMesh.CalculatePath(outputLocationDictionary[i].position, deliveryLocationDictionary[-j].position, NavMesh.AllAreas, path);
                for (int k = 0; k < path.corners.Length - 1; k++)
                {
                    distance += Vector3.Distance(path.corners[k], path.corners[k + 1]);
                    Debug.DrawLine(path.corners[k], path.corners[k + 1], Color.red, 10f, false);
                }
                distances.Add((i, -j), distance);
                //Debug.Log(distance);
            }
            */
        }
        //foreach (var kvp in controller.GetRingStations())
        //{
        //    float temp_min = float.MaxValue;
        //    int id = -1;
        //    foreach (var kvp2 in controller.GetCapStations())
        //    {
        //        if (distances[(kvp.Key, kvp2.Key)] < temp_min)
        //        {
        //            temp_min = distances[(kvp.Key, kvp2.Key)];
        //            id = kvp2.Key;
        //        }   
        //    }
        //    kvp.Value.nearestCapID = (id, temp_min);
        //}

        //foreach (var kvp in controller.GetBaseStations())
        //{
        //    float temp_min = float.MaxValue;
        //    int id = -1;
        //    foreach (var kvp2 in controller.GetCapStations())
        //    {
        //        if (distances[(kvp.Key, kvp2.Key)] < temp_min)
        //        {
        //            temp_min = distances[(kvp.Key, kvp2.Key)];
        //            id = kvp2.Key;
        //        }
        //    }
        //    kvp.Value.nearestCapID = (id, temp_min);
        //}

        return distances;
    }

    public override void OnEpisodeBegin()
    {
        
        transform.rotation = init_rot;
        carrying_p = false;
        carrying_cp = false;
        decided = false;
        agent.ResetPath();
        transform.position = init_pos;
        agent.nextPosition = init_pos;
        agent.destination = init_pos;
        destination = new Vector3();
        //Debug.Log(init_pos);

    }

    


    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {


        //if check dealock autorize that product from blocke ring stations are taken until both products are taken
       
        for (int i = 0; i < n_products+1; i++)
        {
            actionMask.SetActionEnabled(0, i, true);
        }
        actionMask.SetActionEnabled(0, n_products+1, false);
        BaseStation bstation = basestations[1];
        allReady = true;
        bool one_true = false;
        bool prod_wst = false;
        bool deadlock_prio = false;
        int zeros = 1;
        //bool allRequested = true;
        //foreach (var agent in controller.agents)
        //{
        //    if (agent.ID != ID && !agent.decisionRequested && !agent.decided)
        //    {
        //        allRequested = false;
        //    }
        //}
        //bool[] tot = new bool[n_products];
        if (!decided)
        {
            //if (actionBuffers.DiscreteActions[0] != job) { DropProduct(); }

            for (int prod_id = 1; prod_id < n_products + 1; prod_id++)
            {
                var prod = productDictionary[prod_id];
                Workstation station = stations[(int)prod.GetCurrentTask()[0]];

                if (!prod_wst && prod.starting_time > Time.time - controller.GetEpisodeStart())
                {
                    prod_wst = true;
                }
                //if (prod && !prod.assigned && !prod.blocked && !prod.grabbed)

                if (!prod || prod.grabbed || prod.assigned || prod.task_pointer==0)
                {
                    actionMask.SetActionEnabled(0, prod_id, false);
                    zeros += 1;
                }

                else 
                {


                    RingStation rstation = station as RingStation;
                    Workstation prevStation = stations[(int)prod.GetPreviousTask()[0]];
                    RingStation prevRstation = prevStation as RingStation;

                    if (prod && prod.GetCurrStation() && !prod.GetCurrStation().GetComponent<DeliveryStation>()
                        && !prod.GetCurrStation().GetReadyProd() && prod.GetCurrStation().GetNextProd() 
                        && prod.GetCurrStation().GetNextProd().product_ID==prod_id && (!prod.GetCurrStation().GetWaitingProd() ||
                         prod.GetCurrStation().GetWaitingProd().product_ID!=prod_id))
                    {
                        allReady = false;
                    }

                    if (!prod.blocked && ! prod.grabbed && !prod.assigned && (controller.IsInDeadlock() || controller.CheckDeadlock()) && station.inFree && prevStation.outFree)
                    {
                        //if(rstation && prevRstation && rstation.ID==prevRstation.ID && (!rstation.IsAvailable() || !prevRstation.IsAvailable()))
                        //{
                        //    deadlock_prio = true;
                        //}
                        int[] deadlockProds = controller.getDeadlockProds();
                        bool founded = false;
                        foreach (int dP in deadlockProds)
                        {
                            if (dP > 0)
                            {
                                if (dP == prod.product_ID)
                                {
                                    founded = true;
                                }
                            }
                        }
                        if (rstation && prevRstation && rstation.ID != prevRstation.ID && !founded)
                        {
                            actionMask.SetActionEnabled(0, prod_id, false);
                            zeros += 1;
                        }
                        else if (rstation && prevRstation && rstation.ID != prevRstation.ID && (rstation.waitingForComb() || rstation.GetWaitingProd()))
                        {
                            actionMask.SetActionEnabled(0, prod_id, false);
                            zeros += 1;
                        }
                        else if (rstation && !prevRstation)
                        {
                            actionMask.SetActionEnabled(0, prod_id, false);
                            zeros += 1;
                        }
                        else if (!rstation && (!station.IsAvailable() || !station.inFree ||prod.processing))
                        {
                            actionMask.SetActionEnabled(0, prod_id, false);
                            zeros += 1;
                        }
                    }
                    //else if(!prod.assigned &&!prod.grabbed && !prod.blocked() && (prod.JobInSameMachine() && station.WaitingAvailable() || station.IsAvailable()) 
                    //    && station.inFree && prevStation.outFree && !controller.IsInDeadlock())
                    //{
                    //    actionMask.SetActionEnabled(0, prod_id, true);
                    //    one_true = true;
                    //}
                    //((prod.JobInSameMachine() && station.WaitingAvailable()) || station.IsAvailable())
                    else if (prod && ((!station.IsAvailable() && (!prod.JobInSameMachine() || !station.WaitingAvailable())) 
                        || !station.inFree || !prevStation.outFree || prod.processing 
                        || (prod.blocked && !prod.GetCurrWorkstation().IsAvailable()))) // && !prod.assigned
                    {
                        actionMask.SetActionEnabled(0, prod_id, false);
                        zeros += 1;
                    }

                }
            }
        }
        if (!deadlock_prio && !bstation.IsAssigned() && !bstation.IsBlocked() && bstation.outFree && bstation.readyToGive)
        {

            foreach (var kvp in controller.GetStations())
            {
                if (kvp.Value.waitingForComb() && kvp.Value.inFree)
                {
                    actionMask.SetActionEnabled(0, 12, true);
                    zeros -= 1;
                    one_true = true;

                }
            }
        }

        if (zeros == n_products + 1)
        {
            zeros_ite = 0;
        }

        // !allready || allFalse || prod_wst
        if (allReady && zeros !=n_products+1 && !prod_wst) //!allRequested && // && !prod_wst all_ready &&
        {
            actionMask.SetActionEnabled(0, 0, false);
        }

    }


    public override void OnActionReceived(ActionBuffers actionBuffers)
    {

        int action = actionBuffers.DiscreteActions[0];
        int action1 = action - n_products;

        //if check dealock autorize that product from blocke ring stations are taken until both products are taken
        //if (action == 0 && !allReady)
        //{
        //    AddReward(-0.1f);
        //}

        if (!decided)
        {
            if (action > 0 && action1 <= 0)
            {
                //if (actionBuffers.DiscreteActions[0] != job) { DropProduct(); }

                var prod = productDictionary[action];

                //Debug 
                //if (prod)
                //{
                //    int k = 0;
                //    bool temp1 = prod.JobInSameMachine();
                //    bool temp2 = !station.inFree;
                //    bool temp3 = !controller.IsInDeadlock();
                //    int k2 = 0;
                //}
                if (prod && !prod.assigned && !prod.blocked && !prod.grabbed && prod.task_pointer>0)
                {
                    Workstation station = stations[(int)prod.GetCurrentTask()[0]];
                    Workstation prevStation = stations[(int)prod.GetPreviousTask()[0]];
                    if (controller.IsInDeadlock() && station.inFree && prevStation.outFree)
                    {

                        RingStation rstation = station as RingStation;
                        RingStation prevRstation = prevStation as RingStation;
                        if (rstation)
                        {
                            int onlyOne = 0;
                            int[] deadlockProds = controller.getDeadlockProds();
                            int indD = -1;

                            for (int ind = 0; ind < deadlockProds.Length; ind++)
                            {
                                int val = deadlockProds[ind];
                                if (val > 0)
                                {
                                    onlyOne += 1;
                                    if (val == prod.product_ID)
                                    {
                                        indD = ind;
                                    }
                                }
                            }
                            if (indD >-1 && prevRstation && rstation.ID!=prevRstation.ID && rstation.WaitingAvailable())
                            {
                                controller.dealockProdTaken(indD);
                                if (onlyOne == 1)
                                {
                                    controller.DeadlockResolved();
                                }
                                station.AssignInTask((int)prod.GetCurrentTask()[2]);
                                prevStation.AssignOutTask();
                                prod.assigned = true;
                                jobProduct = action;
                                destination = prod.GetDestination(jobProduct, inputLocationDictionary);
                                destination.y = init_pos.y;

                                decided = true;


                                // AddReward(-1f);
                                // FIX 001

                                agent.destination = destination;
                            }
                            else if (prevRstation && rstation.WaitingAvailable())
                            {
                                station.AssignInTask((int)prod.GetCurrentTask()[2]);
                                prevStation.AssignOutTask();
                                prod.assigned = true;
                                jobProduct = action;
                                destination = prod.GetDestination(jobProduct, inputLocationDictionary);
                                destination.y = init_pos.y;

                                decided = true;

                                agent.destination = destination;
                            }
                        }
                        else if (station.IsAvailable() && station.inFree && prevStation.outFree && (prod.task_pointer>1 || basestations[1].readyToGive))
                        {
                            station.AssignInTask((int)prod.GetCurrentTask()[2]);
                            prevStation.AssignOutTask();
                            prod.assigned = true;
                            jobProduct = action;
                            decided = true;
                            if (prod.task_pointer == 1)
                            {
                                controller.AddProductTime(prod.product_ID, basestations[1].preparationFinished);
                            }
                            destination = prod.GetDestination(jobProduct, inputLocationDictionary);
                            destination.y = init_pos.y;
                            agent.destination = destination;

                        }
                        else
                        {
                            agent.destination = init_pos;
                        }
                    }
                    else if (controller.CheckDeadlock() && station.inFree && prevStation.outFree)
                    {

                        RingStation rstation = station as RingStation;
                        RingStation prevRstation = prevStation as RingStation;
                        if (rstation)
                        {
                            int onlyOne = 0;
                            int[] deadlockProds = controller.getDeadlockProds();
                            int indD = -1;

                            for (int ind = 0; ind < deadlockProds.Length; ind++)
                            {
                                int val = deadlockProds[ind];
                                if (val > 0)
                                {
                                    onlyOne += 1;
                                    if (val == prod.product_ID)
                                    {
                                        indD = ind;
                                    }
                                }
                            }
                            if (indD>-1 && prevRstation && rstation.ID != prevRstation.ID && rstation.WaitingAvailable())
                            {
                                controller.dealockProdTaken(indD);
                                if (onlyOne == 1)
                                {
                                    controller.DeadlockResolved();
                                }
                                station.AssignInTask((int)prod.GetCurrentTask()[2]);
                                prevStation.AssignOutTask();
                                prod.assigned = true;
                                jobProduct = action;
                                destination = prod.GetDestination(jobProduct, inputLocationDictionary);
                                destination.y = init_pos.y;


                                decided = true;


                                // AddReward(-1f);
                                // FIX 001
                                agent.destination = destination;
                            }
                            else if (prevRstation && rstation.WaitingAvailable())
                            {
                                station.AssignInTask((int)prod.GetCurrentTask()[2]);
                                prevStation.AssignOutTask();
                                prod.assigned = true;
                                jobProduct = action;
                                destination = prod.GetDestination(jobProduct, inputLocationDictionary);
                                destination.y = init_pos.y;

                                decided = true;

                                //zeros_ite += 1;
                                agent.destination = destination;
                            }
                        }
                        else if (station.IsAvailable() && station.inFree && prevStation.outFree && (prod.task_pointer > 1 || basestations[1].readyToGive))
                        {
                            station.AssignInTask((int)prod.GetCurrentTask()[2]);
                            prevStation.AssignOutTask();
                            prod.assigned = true;
                            jobProduct = action;
                            destination = prod.GetDestination(jobProduct, inputLocationDictionary);
                            destination.y = init_pos.y;


                            decided = true;
                            if (prod.task_pointer == 1)
                            {
                                controller.AddProductTime(prod.product_ID, basestations[1].preparationFinished);
                            }
                            // AddReward(-1f);
                            // FIX 001
                            //zeros_ite = 1;
                            agent.destination = destination;
                        }
                        else
                        {
                            //zeros_ite += 1;
                            agent.destination = init_pos;
                        }
                    }
                    else if (((prod.JobInSameMachine() && station.WaitingAvailable()) || station.IsAvailable()) && station.inFree && prevStation.outFree 
                        && (prod.task_pointer > 1 || basestations[1].readyToGive))
                    {
                        station.AssignInTask((int)prod.GetCurrentTask()[2]);
                        prevStation.AssignOutTask();
                        prod.assigned = true;
                        jobProduct = action;
                        destination = prod.GetDestination(jobProduct, inputLocationDictionary);
                        destination.y = init_pos.y;

                        decided = true;
                        if (prod.task_pointer == 1)
                        {
                            controller.AddProductTime(prod.product_ID, basestations[1].preparationFinished);
                        }
                        //zeros_ite = 1;
                        agent.destination = destination;
                    }
                    else
                    {
                        //zeros_ite += 1;
                        agent.destination = init_pos;
                    }
                }
                else
                {
                    //zeros_ite += 1;
                    agent.destination = init_pos;
                }

            }
            else if (action1 > 0)
            {

                BaseStation bstation = basestations[action1];

                if (!bstation.IsAssigned() && bstation.outFree && bstation.readyToGive)
                {

                    if (mWaiting == -1)
                    {
                        foreach (var kvp in controller.GetStations())
                        {
                            if (kvp.Value.waitingForComb() && kvp.Value.inFree)
                            {
                                mWaiting = kvp.Key;
                                kvp.Value.WillReceive(1);
                                break;
                            }
                        }
                    }
                }
                if (mWaiting != -1)
                {
                    destination = outputLocationDictionary[basestationIDs[action1]].position;
                    bstation.AssignOutTask();
                    controller.GetBaseStationDictionary()[action1].Assign(true);
                    //CombProduct cP = basestations[action1].GetCurrCombProd();
                    //jobComb = cP.combProduct_ID;
                    destination.y = init_pos.y;
                    //if (cP != null && !cP.IsBlocked() && !cP.grabbed)
                    //{
                    //    decided = true;
                    //}
                    //zeros_ite = 1;
                    decided = true;
                    agent.destination = destination;
                }
                /*
                else
                {
                    action2 = 0;
                }
                */
            }
            else
            {
                //zeros_ite += 1;
                agent.destination = init_pos;
            }
            //if(action > 0) {
            //    Debug.Log("action was chosen:" + action);
            //}
        }
        else
        {
            //zeros_ite += 1;
            agent.destination = init_pos;
        }
        if (action!=0 && agent.destination != init_pos)
        {
            zeros_ite = 1;
        }
        else
        {
            zeros_ite += 1;
        }

        last_action = action;
    }


    /// <summary>
    /// Called when and action is received from either the player input or the neural network
    /// </summary>
    

    /// <summary>
    /// Collect vector observations from the environment
    /// </summary>
    /// <param name="sensor">The vector sensor</param>
    public override void CollectObservations(VectorSensor sensor)
    {
        List<float> observations = new List<float>();
        observations = controller.GetLBObservations(ID, transform.position);

        for(int i = 0; i < observations.Count; i++)
        {
            sensor.AddObservation(observations[i]);
        }
    }


    /// <summary>
    /// When Behavior Type is set to "Heuristic Only" on the agent's Behavior Parameters,
    /// this function will be called. Its return values will be fed into
    /// <see cref="OnActionReceived(float[])"/> instead of using the neural network
    /// </summary>
    /// <param name="actionsOut">And output action array</param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (manual)
        {
            if (Input.GetKey(KeyCode.Alpha1))
            {
                discreteActionsOut[0] = 1;
            }
            if (Input.GetKey(KeyCode.Alpha2))
            {
                discreteActionsOut[0] = 2;
            }
            if (Input.GetKey(KeyCode.Alpha3))
            {
                discreteActionsOut[0] = 3;
            }
            if (Input.GetKey(KeyCode.Alpha4))
            {
                discreteActionsOut[0] = 4;
            }
            if (Input.GetKey(KeyCode.Alpha5))
            {
                discreteActionsOut[0] = 5;
            }
            if (Input.GetKey(KeyCode.Alpha6))
            {
                discreteActionsOut[0] = 6;
            }
            if (Input.GetKey(KeyCode.Alpha7))
            {
                discreteActionsOut[0] = 7;
            }
            if (Input.GetKey(KeyCode.Alpha8))
            {
                discreteActionsOut[0] = 8;
            }
            if (Input.GetKey(KeyCode.Alpha9))
            {
                discreteActionsOut[0] = 9;
            }
        }
        if (SPT && !decided)
        {
            float current_time = -1;
            Product p;
            int chosen = 0;
            int chosen2 = 0;
            int not_assigned = 0;
            int otherAgentAction = -1;

            foreach (KeyValuePair<int,JSSPMultiAgent> ag in controller.agents)
            {
                if (ag.Key != ID && ag.Value.decisionRequested)
                {
                    otherAgentAction = ag.Value.stepAction;
                }
            }

            foreach (var kvp in controller.GetBaseStationDictionary())
            {
                if (kvp.Value.outFree && kvp.Value.readyToGive)
                {
                    not_assigned = kvp.Key;
                }
            }

            if (not_assigned > 0 && basestations[1].readyToGive)
            {
                foreach (var kvp in controller.GetStations())
                {
                    if (kvp.Value.waitingForComb() && kvp.Value.inFree)
                    {
                        chosen2 = not_assigned;
                        //mWaiting = kvp.Key;
                        //kvp.Value.WillReceive(1);
                        break;
                    }
                }
            }
            if ((controller.IsInDeadlock()||controller.CheckDeadlock()) && chosen2==0)
            {
                for (int i = 0; i < productDictionary.Count; i++)
                {
                    if (productDictionary[i + 1] && otherAgentAction!=i+1)
                    {
                        p = productDictionary[i + 1];
                        

                        if (!p.assigned && !p.grabbed)
                        {
                            Workstation station = controller.GetStations()[(int)p.GetCurrentTask().x];
                            int task_ID = (int)p.GetCurrentTask().x;
                            RingStation rstation = station as RingStation;
                            if (rstation)
                            {
                                int[] deadlockProds = controller.getDeadlockProds();
                                int indD = -1;

                                for (int ind = 0; ind < deadlockProds.Length; ind++)
                                {
                                    int val = deadlockProds[ind];
                                    if (val > 0)
                                    {
                                        if (val == p.product_ID)
                                        {
                                            indD = ind;
                                        }
                                    }
                                }

                                if (indD>-1 && !p.blocked)
                                {
                                    int ind = (int)p.GetPreviousTask()[0];
                                    Workstation prevStation = controller.GetStations()[ind];
                                    float temp_task_time = Time.time + controller.GetStationDistances()[(ind, task_ID)] / speed;//check if task_ID task or station_id
                                    temp_task_time += p.GetCurrentTask().y;
                                    if (prevStation.GetNextProd() && prevStation.GetNextProd().product_ID == p.product_ID
                                        && (current_time < 0 || current_time > temp_task_time) && prevStation.outFree && station.inFree && !station.GetWaitingProd())
                                    {
                                        stepAction = p.product_ID;
                                        current_time = temp_task_time;
                                        chosen = p.product_ID;
                                    }
                                }
                                else if (!p.blocked) //!p.LastMachine()
                                {
                                    int ind = (int)p.GetPreviousTask()[0];
                                    Workstation prevStation = controller.GetStations()[ind];
                                    float temp_task_time = Time.time + controller.GetStationDistances()[(ind, task_ID)] / speed;//check if task_ID task or station_id
                                    temp_task_time += p.GetCurrentTask().y;
                                    if (prevStation.GetNextProd() && prevStation.GetNextProd().product_ID == p.product_ID &&
                                        prevStation.outFree && station.inFree && (p.JobInSameMachine() || (p.LastMachine() && !station.GetWaitingProd()) || station.IsAvailable())
                                        && (current_time < 0 || current_time > temp_task_time))
                                    {
                                        stepAction = p.product_ID;
                                        current_time = temp_task_time;
                                        chosen = p.product_ID;
                                    }
                                }
                                else if (!p.LastMachine() && station.GetNextProd() && station.GetNextProd().product_ID == p.product_ID)
                                {
                                    float expectedTaskTime = Time.time + p.RemainingTime();
                                    int ind = (int)p.GetNextTask()[0];
                                    Workstation nextStation = controller.GetStations()[ind];
                                    expectedTaskTime += controller.GetStationDistances()[(task_ID, ind)]/speed;
                                    expectedTaskTime += p.GetNextTask().y;
                                    if (nextStation.IsAvailable() && (current_time < 0 || current_time > expectedTaskTime))
                                    {
                                        stepAction = p.product_ID;
                                        current_time = expectedTaskTime;
                                        chosen = 0;
                                    }
                                }

                            }
                            else
                            {
                                if ((p.task_pointer == 0 || p.task_pointer == 1 &&
                                controller.GetProductsTimes()[p.product_ID-1].Count == 0))
                                {
                                    float temp_task_time = 0;
                                    NavMesh.CalculatePath(transform.position,
                                        basestations[1].output_location.position, NavMesh.AllAreas, path);
                                    for (int j = 0; j < path.corners.Length - 1; j++)
                                    {
                                        temp_task_time += Vector3.Distance(path.corners[j], path.corners[j + 1]) / speed;

                                    }

                                    float expectedTime = basestations[1].ReadyTime();
                                    expectedTime = Mathf.Max(expectedTime, p.starting_time);
                                    expectedTime = Mathf.Max(Time.time + temp_task_time - startTime, expectedTime);
                                    if ((current_time < 0 || current_time > expectedTime) && basestations[1].outFree 
                                         && station.inFree && station.IsAvailable() )
                                    {
                                        stepAction = p.product_ID;
                                        current_time = expectedTime;
                                        chosen = 0;
                                        if (basestations[1].readyToGive && !p.blocked)
                                        {
                                            chosen = p.product_ID;
                                        }
                                    }

                                }
                                else
                                {
                                    if (!p.blocked)
                                    {
                                        int ind = (int)p.GetPreviousTask()[0];
                                        Workstation prevStation = controller.GetStations()[ind];
                                        float temp_task_time = Time.time + controller.GetStationDistances()[(ind, task_ID)]/speed;//check if task_ID task or station_id
                                        temp_task_time += p.GetCurrentTask().y;
                                        if (prevStation.GetNextProd() && prevStation.GetNextProd().product_ID == p.product_ID &&
                                            prevStation.outFree && station.inFree && (p.JobInSameMachine() || (p.LastMachine() && !station.GetWaitingProd()) || station.IsAvailable())
                                            && (current_time < 0 || current_time > temp_task_time))
                                        {
                                            stepAction = p.product_ID;
                                            current_time = temp_task_time;
                                            chosen = p.product_ID;
                                        }
                                    }
                                    else if (!p.LastMachine() && station.GetNextProd() && station.GetNextProd().product_ID == p.product_ID)
                                    {
                                        float expectedTaskTime = Time.time + p.RemainingTime();
                                        int ind = (int)p.GetNextTask()[0];
                                        Workstation nextStation = controller.GetStations()[ind];  //need to check if nextStation is in the deadlock process
                                        expectedTaskTime += controller.GetStationDistances()[(task_ID, ind)]/speed;
                                        expectedTaskTime += p.GetNextTask().y;
                                        if (nextStation.IsAvailable() && (current_time < 0 || current_time > expectedTaskTime))
                                        {
                                            stepAction = p.product_ID;
                                            current_time = expectedTaskTime;
                                            chosen = 0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (chosen2 == 0 && chosen == 0)
            {
                for (int i = 0; i < productDictionary.Count; i++)
                {
                    if (productDictionary[i + 1] && otherAgentAction != i + 1)
                    {
                        p = productDictionary[i + 1];
                        //var temp2 = p.job;
                        Workstation station = controller.GetStations()[(int)p.GetCurrentTask().x];
                        int task_ID = (int)p.GetCurrentTask().x;
                        //float currTaskTime = Time.time - startTime;
                        //case where p already in deliverystation needs to be considered
                        if (!p.assigned && !p.grabbed)
                        {
                            if ((p.task_pointer == 0 || p.task_pointer == 1 &&
                                controller.GetProductsTimes()[p.product_ID-1].Count == 0))
                            {
                                float temp_task_time = 0;
                                NavMesh.CalculatePath(transform.position,
                                    basestations[1].output_location.position, NavMesh.AllAreas, path);
                                for (int j = 0; j < path.corners.Length - 1; j++)
                                {
                                    temp_task_time += Vector3.Distance(path.corners[j], path.corners[j + 1]) / speed;

                                }

                                float expectedTime = basestations[1].ReadyTime();
                                expectedTime = Mathf.Max(expectedTime, p.starting_time);
                                expectedTime = Mathf.Max(Time.time + temp_task_time - startTime, expectedTime);
                                if ((current_time < 0 || current_time > expectedTime) && basestations[1].outFree && station.inFree && station.IsAvailable() )
                                {
                                    stepAction = p.product_ID;
                                    current_time = expectedTime;
                                    chosen = 0;
                                    if ( basestations[1].readyToGive && !p.blocked)
                                    {
                                        chosen = p.product_ID;
                                    }
                                }

                            }
                            else 
                            {
                                if (!p.blocked)
                                {
                                    int ind = (int)p.GetPreviousTask()[0];
                                    Workstation prevStation = controller.GetStations()[ind];
                                    float temp_task_time =Time.time + controller.GetStationDistances()[(ind, task_ID)]/speed;//check if task_ID task or station_id
                                    temp_task_time += p.GetCurrentTask().y;
                                    if (prevStation.GetNextProd() && prevStation.GetNextProd().product_ID==p.product_ID && prevStation.outFree && station.inFree && 
                                        (p.JobInSameMachine() || (p.LastMachine() && !station.GetWaitingProd()) || station.IsAvailable()) &&
                                        (current_time < 0 || current_time > temp_task_time))
                                    {
                                        stepAction = p.product_ID;
                                        current_time = temp_task_time;
                                        chosen = p.product_ID;
                                    }
                                }
                                else if (!p.LastMachine() && station.GetNextProd() && station.GetNextProd().product_ID==p.product_ID)
                                {
                                    float expectedTaskTime = Time.time + p.RemainingTime();
                                    int ind = (int)p.GetNextTask()[0];
                                    Workstation nextStation = controller.GetStations()[ind];
                                    expectedTaskTime += controller.GetStationDistances()[(task_ID, ind)]/speed;
                                    expectedTaskTime += p.GetNextTask().y;
                                    if (nextStation.IsAvailable() && (current_time < 0 || current_time > expectedTaskTime))
                                    {
                                        stepAction = p.product_ID;
                                        current_time = expectedTaskTime;
                                        chosen = 0;
                                    }
                                }
                            }
                        }
                    } //(p.starting_time < currDeltaTime && !p.grabbed && !p.assigned && !p.blocked && station.inFree && (p.JobInSameMachine() || p.LastMachine() || station.IsAvailable()))
                
                }
            }

            discreteActionsOut[0] = 0;
            if (chosen > 0)
            {
                discreteActionsOut[0] = chosen;
                //stepAction = chosen;
            }
            else if (chosen2 > 0)
            {
                discreteActionsOut[0] = n_products + chosen2;
            }
        }

        //if (SPT)
        //{
        //    float current_time = -1;
        //    Product p;
        //    int chosen = 0;
        //    int chosen2 = 0;
        //    int not_assigned = 0;


        //    foreach (var kvp in controller.GetBaseStationDictionary())
        //    {
        //        if (!kvp.Value.IsAssigned())
        //        {
        //            not_assigned = kvp.Key;
        //        }
        //    }
        //    if (not_assigned > 0)
        //    {
        //        foreach (var kvp in controller.GetStations())
        //        {
        //            if (kvp.Value.waitingForComb())
        //            {
        //                chosen2 = not_assigned;
        //                //mWaiting = kvp.Key;
        //                //kvp.Value.WillReceive(1);
        //                break;
        //            }
        //        }
        //    }
        //    if (controller.IsInDeadlock())
        //    {
        //        int onlyOne = 0;
        //        foreach (var kvp in controller.GetRingStations())
        //        {
        //            p = kvp.Value.GetNextProd();
        //            if (p && !p.blocked && !p.assigned)
        //            {
        //                chosen = p.product_ID;
        //                onlyOne++;
        //            }
        //        }
        //        /*
        //        if (onlyOne == 1)
        //        {
        //            controller.DeadlockResolved();
        //        }
        //        */
        //    }
        //    if (!controller.IsInDeadlock() && chosen2 == 0 && chosen == 0)
        //    {
        //        float currDeltaTime = Time.time - startTime;
        //        for (int i = 0; i < productDictionary.Count; i++)
        //        {
        //            if (productDictionary[i + 1])
        //            {
        //                p = productDictionary[i + 1];
        //                // Check if the product is being worked on
        //                //var temp2 = controller.GetStations()[(int)p.GetNextTask().x];
        //                int temp = (int)p.GetCurrentTask().x;
        //                var temp2 = p.job;
        //                Workstation station = controller.GetStations()[(int)p.GetCurrentTask().x];
        //                int ind = (int)p.GetPreviousTask()[0];
        //                RingStation r_station = null;
        //                BaseStation b_station = null;
        //                int task_ID = (int)p.GetCurrentTask().x;
        //                float temp_t = p.starting_time;

        //                if (ind > 0)
        //                {
        //                    r_station = controller.GetStations()[ind] as RingStation;
        //                }
        //                /*
        //                if (task_ID == 4)
        //                {
        //                    int temp_ID = -1;
        //                    b_station = controller.GetStations()[ind] as BaseStation;
        //                    if (r_station)
        //                    {
        //                        if (!controller.GetStations()[r_station.nearestCapID.Item1].IsProcessing())
        //                        {
        //                            if (r_station.nearestCapID.Item1 == 4)
        //                            {
        //                               temp_ID = 5;
        //                            }
        //                            else
        //                            {
        //                                temp_ID = 4;

        //                            }
        //                        }


        //                    }
        //                    else if (b_station)
        //                    {
        //                        if (!controller.GetStations()[b_station.nearestCapID.Item1].IsProcessing())
        //                        {
        //                            if (b_station.nearestCapID.Item1 == 4)
        //                            {
        //                                temp_ID = 5;
        //                            }
        //                            else
        //                            {
        //                                temp_ID = 4;

        //                            }
        //                        }
        //                    }
        //                    task_ID = temp_ID;
        //                }
        //                */
        //                if (p.starting_time < currDeltaTime && !p.grabbed && !p.assigned && !p.blocked && station.inFree &&
        //                    ( p.JobInSameMachine() || p.LastMachine() || station.IsAvailable()))
        //                {
        //                    //float currTransportTime = CalculateDistanceTime(task_ID);
        //                    float taskTime = CalculateTimeForTask(p.GetCurrentTask());
        //                    if (task_ID > 0)
        //                    { // Workstation Task
        //                        if (current_time < 0 && inputLocationDictionary[task_ID].gameObject.activeSelf)
        //                        {
        //                            current_time = taskTime;
        //                            chosen = p.product_ID;
        //                        }
        //                        else if (taskTime <= current_time && inputLocationDictionary[task_ID].gameObject.activeSelf)
        //                        {
        //                            current_time = taskTime;
        //                            chosen = p.product_ID;
        //                        }
        //                    }
        //                    //check Deadlock
        //                }
        //                else if (r_station && !p.grabbed && !p.assigned && !p.blocked && controller.CheckDeadlock())
        //                {
        //                    //float currTransportTime = CalculateDistanceTime(task_ID);
        //                    float taskTime = CalculateTimeForTask(p.GetCurrentTask());
        //                    if (current_time < 0 && inputLocationDictionary[(int)p.GetCurrentTask().x].gameObject.activeSelf)
        //                    {
        //                        current_time = taskTime;
        //                        chosen = p.product_ID;
        //                        break;
        //                    }
        //                    else if (taskTime <= current_time && inputLocationDictionary[(int)p.GetCurrentTask().x].gameObject.activeSelf)
        //                    {
        //                        current_time = taskTime;
        //                        chosen = p.product_ID;
        //                        break;
        //                    }

        //                }
        //            }


        //        }
        //    }

        //    //Debug.Log(inputLocationDictionary[(int)current_task].gameObject.activeSelf);
        //    /*
        //    if (chosen2 > 0)
        //    {
        //        if (controller.GetBaseStationDictionary()[not_assigned].IsAssigned())
        //        {
        //            chosen2 = 0;
        //        }
        //    }
        //    */
        //    /*
        //    if (chosen2 > 0)
        //    {
        //        controller.GetBaseStationDictionary()[not_assigned].Assign(true);
                
        //    }
        //    if (chosen > 0)
        //    {
        //        p = productDictionary[chosen];
        //        if (p.assigned)
        //        {
        //            chosen = 0;
        //        }
        //        else
        //        {
        //            p.assigned = true;
        //            Workstation station = controller.GetStations()[(int)p.GetCurrentTask().x];
        //            station.Assign();
        //            station.inFree = true;
        //            if (controller.IsInDeadlock())
        //            {
        //                var temp = controller.GetStations()[(int)p.GetPreviousTask()];
        //                controller.GetStations()[(int)p.GetPreviousTask()].Assign();
        //            }
        //            //var temp = controller.GetStations()[(int)p.GetCurrentTask().x];
        //            /*
        //            //BaseStation b_parent = controller.GetStations()[(int)p.GetCurrentTask().x] as BaseStation;
        //            DeliveryStation d_parent = controller.GetStations()[(int)p.GetCurrentTask().x] as DeliveryStation;
        //            if (!d_parent)
        //            {
        //                controller.GetStations()[(int)p.GetCurrentTask().x].Assign();
        //            }
                    
        //        }
                 
        //    }
        //    */
        //    discreteActionsOut[0] = 0;
        //    if (chosen > 0)
        //    {
        //        discreteActionsOut[0] = chosen;
        //    }
        //    else if (chosen2 > 0)
        //    {
        //        discreteActionsOut[0] = n_products + chosen2;
        //    }
        //}

        //if (SPT2)
        //{
        //    float current_time = -1;
        //    Product p;
        //    int chosen = 0;
        //    int chosen2 = 0;
        //    int not_assigned = 0;


        //    foreach (var kvp in controller.GetBaseStationDictionary())
        //    {
        //        if (!kvp.Value.IsAssigned())
        //        {
        //            not_assigned = kvp.Key;
        //        }
        //    }
        //    if (not_assigned > 0)
        //    {
        //        foreach (var kvp in controller.GetStations())
        //        {
        //            if (kvp.Value.waitingForComb())
        //            {
        //                chosen2 = not_assigned;
        //                //mWaiting = kvp.Key;
        //                //kvp.Value.WillReceive(1);
        //                break;
        //            }
        //        }
        //    }
        //    if (controller.IsInDeadlock())
        //    {
        //        int onlyOne = 0;
        //        foreach (var kvp in controller.GetRingStations())
        //        {
        //            p = kvp.Value.GetNextProd();
        //            if (p && !p.blocked && !p.assigned)
        //            {
        //                chosen = p.product_ID;
        //                onlyOne++;
        //            }
        //        }
        //        /*
        //        if (onlyOne == 1)
        //        {
        //            controller.DeadlockResolved();
        //        }
        //        */
        //    }
        //    if (!controller.IsInDeadlock() && chosen2 == 0 && chosen == 0)
        //    {
        //        float currDeltaTime = Time.time - startTime;
        //        for (int i = 0; i < productDictionary.Count; i++)
        //        {
        //            if (productDictionary[i + 1])
        //            {
        //                p = productDictionary[i + 1];
        //                // Check if the product is being worked on
        //                //var temp2 = controller.GetStations()[(int)p.GetNextTask().x];
        //                int temp = (int)p.GetCurrentTask().x;
        //                var temp2 = p.job;
        //                Workstation station = controller.GetStations()[(int)p.GetCurrentTask().x];
        //                int ind = p.GetPreviousTask();
        //                RingStation r_station = null;
        //                BaseStation b_station = null;
        //                int task_ID = (int)p.GetCurrentTask().x;
        //                float temp_t = p.starting_time;

        //                if (ind > 0)
        //                {
        //                    r_station = controller.GetStations()[ind] as RingStation;
        //                }

                        
        //                if (p.starting_time < currDeltaTime && !p.grabbed && !p.assigned && !p.blocked && station.inFree &&
        //                    (p.JobInSameMachine() || p.LastMachine() || station.IsAvailable()))
        //                {
        //                    //float currTransportTime = CalculateDistanceTime(task_ID);
        //                    float taskTime = CalculateTimeForTask(p.GetCurrentTask());
        //                    if (task_ID > 0)
        //                    { // Workstation Task
        //                        if (current_time < 0 && inputLocationDictionary[task_ID].gameObject.activeSelf)
        //                        {
        //                            current_time = taskTime;
        //                            chosen = p.product_ID;
        //                        }
        //                        else if (taskTime <= current_time && inputLocationDictionary[task_ID].gameObject.activeSelf)
        //                        {
        //                            current_time = taskTime;
        //                            chosen = p.product_ID;
        //                        }
        //                    }
        //                    //check Deadlock
        //                }
        //                else if (r_station && !p.grabbed && !p.assigned && !p.blocked && controller.CheckDeadlock())
        //                {
        //                    //float currTransportTime = CalculateDistanceTime(task_ID);
        //                    float taskTime = CalculateTimeForTask(p.GetCurrentTask());
        //                    if (current_time < 0 && inputLocationDictionary[(int)p.GetCurrentTask().x].gameObject.activeSelf)
        //                    {
        //                        current_time = taskTime;
        //                        chosen = p.product_ID;
        //                        break;
        //                    }
        //                    else if (taskTime <= current_time && inputLocationDictionary[(int)p.GetCurrentTask().x].gameObject.activeSelf)
        //                    {
        //                        current_time = taskTime;
        //                        chosen = p.product_ID;
        //                        break;
        //                    }

        //                }
        //            }


        //        }
        //    }

        //    discreteActionsOut[0] = 0;
        //    if (chosen > 0)
        //    {
        //        discreteActionsOut[0] = chosen;
        //    }
        //    else if (chosen2 > 0)
        //    {
        //        discreteActionsOut[0] = n_products + chosen2;
        //    }
        //}


        //Shortest Processing Time PDR
        if (LPT && !decided) 
        {
            float current_time = -1;
            Product p;
            int chosen = 0;
            int chosen2 = 0;
            int not_assigned = 0;
            int otherAgentAction = -1;

            foreach (KeyValuePair<int, JSSPMultiAgent> ag in controller.agents)
            {
                if (ag.Key != ID && ag.Value.decisionRequested)
                {
                    otherAgentAction = ag.Value.stepAction;
                }
            }

            foreach (var kvp in controller.GetBaseStationDictionary())
            {
                if (kvp.Value.outFree && kvp.Value.readyToGive)
                {
                    not_assigned = kvp.Key;
                }
            }

            if (not_assigned > 0 && basestations[1].readyToGive)
            {
                foreach (var kvp in controller.GetStations())
                {
                    if (kvp.Value.waitingForComb() && kvp.Value.inFree)
                    {
                        chosen2 = not_assigned;
                        //mWaiting = kvp.Key;
                        //kvp.Value.WillReceive(1);
                        break;
                    }
                }
            }
            if ((controller.IsInDeadlock() || controller.CheckDeadlock()) && chosen2 == 0)
            {
                for (int i = 0; i < productDictionary.Count; i++)
                {
                    if (productDictionary[i + 1] && otherAgentAction != i + 1)
                    {
                        p = productDictionary[i + 1];


                        if (!p.assigned && !p.grabbed)
                        {
                            Workstation station = controller.GetStations()[(int)p.GetCurrentTask().x];
                            int task_ID = (int)p.GetCurrentTask().x;
                            RingStation rstation = station as RingStation;
                            if (rstation)
                            {
                                int[] deadlockProds = controller.getDeadlockProds();
                                int indD = -1;

                                for (int ind = 0; ind < deadlockProds.Length; ind++)
                                {
                                    int val = deadlockProds[ind];
                                    if (val > 0)
                                    {
                                        if (val == p.product_ID)
                                        {
                                            indD = ind;
                                        }
                                    }
                                }

                                if (indD > -1 && !p.blocked)
                                {
                                    int ind = (int)p.GetPreviousTask()[0];
                                    Workstation prevStation = controller.GetStations()[ind];
                                    float temp_task_time = Time.time + controller.GetStationDistances()[(ind, task_ID)] / speed;//check if task_ID task or station_id
                                    temp_task_time += p.GetCurrentTask().y;
                                    if (prevStation.GetNextProd() && prevStation.GetNextProd().product_ID == p.product_ID
                                        && (current_time < 0 || current_time > temp_task_time) && prevStation.outFree && station.inFree && !station.GetWaitingProd())
                                    {
                                        stepAction = p.product_ID;
                                        current_time = temp_task_time;
                                        chosen = p.product_ID;
                                    }
                                }
                                else if (!p.blocked) //!p.LastMachine()
                                {
                                    int ind = (int)p.GetPreviousTask()[0];
                                    Workstation prevStation = controller.GetStations()[ind];
                                    float temp_task_time = Time.time + controller.GetStationDistances()[(ind, task_ID)] / speed;//check if task_ID task or station_id
                                    temp_task_time += p.GetCurrentTask().y;
                                    if (prevStation.GetNextProd() && prevStation.GetNextProd().product_ID == p.product_ID &&
                                        prevStation.outFree && station.inFree && (p.JobInSameMachine() || (p.LastMachine() && !station.GetWaitingProd()) || station.IsAvailable())
                                        && (current_time < 0 || current_time < temp_task_time))
                                    {
                                        stepAction = p.product_ID;
                                        current_time = temp_task_time;
                                        chosen = p.product_ID;
                                    }
                                }
                                else if (!p.LastMachine() && station.GetNextProd() && station.GetNextProd().product_ID == p.product_ID)
                                {
                                    float expectedTaskTime = Time.time;
                                    int ind = (int)p.GetNextTask()[0];
                                    Workstation nextStation = controller.GetStations()[ind];
                                    expectedTaskTime += controller.GetStationDistances()[(task_ID, ind)] / speed;
                                    expectedTaskTime += p.GetNextTask().y;
                                    if (nextStation.IsAvailable() && (current_time < 0 || current_time < expectedTaskTime))
                                    {
                                        stepAction = p.product_ID;
                                        current_time = expectedTaskTime;
                                        chosen = 0;
                                    }
                                }

                            }
                            else
                            {
                                if ((p.task_pointer == 0 || p.task_pointer == 1 &&
                                controller.GetProductsTimes()[p.product_ID - 1].Count == 0))
                                {
                                    float temp_task_time = 0;
                                    NavMesh.CalculatePath(transform.position,
                                        basestations[1].output_location.position, NavMesh.AllAreas, path);
                                    for (int j = 0; j < path.corners.Length - 1; j++)
                                    {
                                        temp_task_time += Vector3.Distance(path.corners[j], path.corners[j + 1]) / speed;

                                    }

                                    float expectedTime = basestations[1].ReadyTime();
                                    expectedTime = Mathf.Max(expectedTime, p.starting_time);
                                    expectedTime = Mathf.Max(Time.time + temp_task_time - startTime, expectedTime);
                                    if ((current_time < 0 || current_time < expectedTime) && basestations[1].outFree
                                         && station.inFree && station.IsAvailable())
                                    {
                                        stepAction = p.product_ID;
                                        current_time = expectedTime;
                                        chosen = 0;
                                        if (basestations[1].readyToGive && !p.blocked)
                                        {
                                            chosen = p.product_ID;
                                        }
                                    }

                                }
                                else
                                {
                                    if (!p.blocked)
                                    {
                                        int ind = (int)p.GetPreviousTask()[0];
                                        Workstation prevStation = controller.GetStations()[ind];
                                        float temp_task_time = Time.time + controller.GetStationDistances()[(ind, task_ID)] / speed;//check if task_ID task or station_id
                                        temp_task_time += p.GetCurrentTask().y;
                                        if (prevStation.GetNextProd() && prevStation.GetNextProd().product_ID == p.product_ID &&
                                            prevStation.outFree && station.inFree && (p.JobInSameMachine() || (p.LastMachine() && !station.GetWaitingProd()) || station.IsAvailable())
                                            && (current_time < 0 || current_time < temp_task_time))
                                        {
                                            stepAction = p.product_ID;
                                            current_time = temp_task_time;
                                            chosen = p.product_ID;
                                        }
                                    }
                                    else if (!p.LastMachine() && station.GetNextProd() && station.GetNextProd().product_ID == p.product_ID)
                                    {
                                        float expectedTaskTime = Time.time;
                                        int ind = (int)p.GetNextTask()[0];
                                        Workstation nextStation = controller.GetStations()[ind];  //need to check if nextStation is in the deadlock process
                                        expectedTaskTime += controller.GetStationDistances()[(task_ID, ind)] / speed;
                                        expectedTaskTime += p.GetNextTask().y;
                                        if (nextStation.IsAvailable() && (current_time < 0 || current_time < expectedTaskTime))
                                        {
                                            stepAction = p.product_ID;
                                            current_time = expectedTaskTime;
                                            chosen = 0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (chosen2 == 0 && chosen == 0)
            {
                for (int i = 0; i < productDictionary.Count; i++)
                {
                    if (productDictionary[i + 1] && otherAgentAction != i + 1)
                    {
                        p = productDictionary[i + 1];
                        //var temp2 = p.job;
                        Workstation station = controller.GetStations()[(int)p.GetCurrentTask().x];
                        int task_ID = (int)p.GetCurrentTask().x;
                        //float currTaskTime = Time.time - startTime;
                        //case where p already in deliverystation needs to be considered
                        if (!p.assigned && !p.grabbed)
                        {
                            if ((p.task_pointer == 0 || p.task_pointer == 1 &&
                                controller.GetProductsTimes()[p.product_ID - 1].Count == 0))
                            {
                                float temp_task_time = 0;
                                NavMesh.CalculatePath(transform.position,
                                    basestations[1].output_location.position, NavMesh.AllAreas, path);
                                for (int j = 0; j < path.corners.Length - 1; j++)
                                {
                                    temp_task_time += Vector3.Distance(path.corners[j], path.corners[j + 1]) / speed;

                                }

                                float expectedTime = basestations[1].ReadyTime();
                                expectedTime = Mathf.Max(expectedTime, p.starting_time);
                                expectedTime = Mathf.Max(Time.time + temp_task_time - startTime, expectedTime);
                                if ((current_time < 0 || current_time > expectedTime) && basestations[1].outFree && station.inFree && station.IsAvailable())
                                {
                                    stepAction = p.product_ID;
                                    current_time = expectedTime;
                                    chosen = 0;
                                    if (basestations[1].readyToGive && !p.blocked)
                                    {
                                        chosen = p.product_ID;
                                    }
                                }

                            }
                            else
                            {
                                if (!p.blocked)
                                {
                                    int ind = (int)p.GetPreviousTask()[0];
                                    Workstation prevStation = controller.GetStations()[ind];
                                    float temp_task_time = Time.time + controller.GetStationDistances()[(ind, task_ID)] / speed;//check if task_ID task or station_id
                                    temp_task_time += p.GetCurrentTask().y;
                                    if (prevStation.GetNextProd() && prevStation.GetNextProd().product_ID == p.product_ID && prevStation.outFree && station.inFree &&
                                        (p.JobInSameMachine() || (p.LastMachine() && !station.GetWaitingProd()) || station.IsAvailable()) &&
                                        (current_time < 0 || current_time > temp_task_time))
                                    {
                                        stepAction = p.product_ID;
                                        current_time = temp_task_time;
                                        chosen = p.product_ID;
                                    }
                                }
                                else if (!p.LastMachine() && station.GetNextProd() && station.GetNextProd().product_ID == p.product_ID)
                                {
                                    float expectedTaskTime = Time.time + p.RemainingTime();
                                    int ind = (int)p.GetNextTask()[0];
                                    Workstation nextStation = controller.GetStations()[ind];
                                    expectedTaskTime += controller.GetStationDistances()[(task_ID, ind)] / speed;
                                    expectedTaskTime += p.GetNextTask().y;
                                    if (nextStation.IsAvailable() && (current_time < 0 || current_time > expectedTaskTime))
                                    {
                                        stepAction = p.product_ID;
                                        current_time = expectedTaskTime;
                                        chosen = 0;
                                    }
                                }
                            }
                        }
                    } //(p.starting_time < currDeltaTime && !p.grabbed && !p.assigned && !p.blocked && station.inFree && (p.JobInSameMachine() || p.LastMachine() || station.IsAvailable()))

                }
            }

            discreteActionsOut[0] = 0;
            if (chosen > 0)
            {
                discreteActionsOut[0] = chosen;
                //stepAction = chosen;
            }
            else if (chosen2 > 0)
            {
                discreteActionsOut[0] = n_products + chosen2;
            }
        }

    }

    private void OnCollisionEnter(Collision collision)
    {
        Collider collider = collision.collider;

        //Penalize AGV collisions
        if (collider.CompareTag("AGV"))
        {
            Physics.IgnoreCollision(this.GetComponent<Collider>(), collider);
            //AddReward(-.1f);
        }

        if (!carrying_p && !carrying_cp && !collider.CompareTag("AGV"))
        {
            if (collider.CompareTag("combproduct") && mWaiting>0)
            {
                combProduct = collider.gameObject.GetComponent<CombProduct>();
                GrabCombProduct(combProduct);
            }
            else if (collider.CompareTag("product"))
            {
                product = collider.gameObject.GetComponent<Product>();
                if (product.product_ID == jobProduct)
                {
                    GrabProduct(product);
                }
            }
            else if (collider.CompareTag("table"))
            {
                TemporalStation temp = collider.transform.GetComponent<TemporalStation>();
                if (temp.full)
                {
                    product = temp.GetProduct();
                    GrabProduct(product);
                    temp.full = false;
                }

            }
        }
        else
        {
            if (collider.CompareTag("table"))
            {
                TemporalStation temp = collider.transform.GetComponent<TemporalStation>();

                if (!temp.full)
                {
                    temp.full = true;
                    product.SetinTable(collider.transform);
                    //decided = false;
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TriggerEnterOrStay(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TriggerEnterOrStay(other);
    }

    private void TriggerEnterOrStay(Collider collider)
    {
        //While carrying a product
        if (carrying_cp)
        {
            // Colliding with Workstations
            
            if (collider.CompareTag("w_input"))
            {
                Workstation station = workstationColliderDictionary[collider];
                if ( mWaiting == station.ID)
                {
                    DropCombProduct();
                }
            }
        }
        else if (carrying_p) {
            if (collider.CompareTag("w_input"))
            {
                Workstation station = workstationColliderDictionary[collider];
                if (task == station.ID && station.IsAvailable())
                {
                    //AddReward(2.5f);
                    //collider.gameObject.SetActive(false); // Disables Workstation
                    
                    //workstationColliderDictionary[collider].inFree = false;
                    
                    debug_collider = collider;
                    
                    DropProduct();
                }
            }
        }
        /*
            // Colliding with Delivery Station
            if (collider.CompareTag("d_input"))
            {
                if (task.x == workstationColliderDictionary[collider].ID)
                {
                    //AddReward(2.5f);
                    product.CompleteJob(collider);
                    decided = false;
                    //gameobject.SetActive(false);
                }
            }
            */
        else
        {
            if (collider.CompareTag("combproduct") && mWaiting>0)
            {
                combProduct = collider.gameObject.GetComponent<CombProduct>();
                if (!combProduct.IsGrabbed())
                {
                    GrabCombProduct(combProduct);
                }
            }
            else if (collider.CompareTag("product"))
            {
                product = collider.gameObject.GetComponent<Product>();
                if (product.product_ID == jobProduct && !product.IsGrabbed())
                {
                    GrabProduct(product);
                }
            }
            
        }
    }

    private void GetLocations(Transform parent,bool afterReset)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            // Get Workstation Locations
            Transform child = parent.GetChild(i);
            if(child.CompareTag("basestation"))
            {
                workstation = child.gameObject.GetComponent<Workstation>();
                BaseStation basestation = child.gameObject.GetComponent<BaseStation>();
                Transform input = workstation.input_location;
                Collider coll = workstation.input_collider;
                Transform output = workstation.output_location;
                //Debug.Log("Workstation Read: "+workstation.workstation_ID);
                inputLocationDictionary.Add(workstation.ID, input);
                outputLocationDictionary.Add(workstation.ID, output);
                workstationColliderDictionary.Add(coll, workstation);
                basestationIDs.Add(basestation.base_ID, workstation.ID);
                baseStationID = workstation.ID;
                stations.Add(workstation.ID, workstation);
                basestations.Add(basestation.base_ID, basestation);
            }
            else if ( child.CompareTag("ringstation")|| child.CompareTag("capstation") || child.CompareTag("deliverystation"))
            {
                workstation = child.gameObject.GetComponent<Workstation>();
                Transform input = workstation.input_location;
                Collider coll = workstation.input_collider;
                Transform output = workstation.output_location;
                //Debug.Log("Workstation Read: "+workstation.workstation_ID);
                inputLocationDictionary.Add(workstation.ID, input);
                outputLocationDictionary.Add(workstation.ID, output);
                workstationColliderDictionary.Add(coll, workstation);
                stations.Add(workstation.ID, workstation);
            }
            else if (child.CompareTag("product"))
            {
                Product p = child.gameObject.GetComponent<Product>();
                productDictionary.Add(p.product_ID, p);
            }
            else if (child.CompareTag("AGV") && !afterReset)
            {
                JSSPMultiAgent agent = child.gameObject.GetComponent<JSSPMultiAgent>();
                agentDictionary.Add(agent.ID, agent);
            }

        }
    }

    private void GrabProductold(Product prod)
    {
        Workstation w_parent = prod.GetComponentInParent<Workstation>();
        if (!prod.IsBlocked() && (!w_parent.IsBlocked()))
        {
            w_parent.UnBlock();
            w_parent.GiveProduct();
            if (!prod.JobInSameMachine())
            {
                w_parent.UnAssign();
            }

            /*
            BaseStation b_parent = prod.GetComponentInParent<BaseStation>();
            if (!b_parent) {
                w_parent.UnAssign();
            }
            */

            prod.SetPosition(load.position);
            prod.SetParent(transform);

            task = (int)prod.GetCurrentTask().x;
            prod.grabbed = true;
            grabbedProductID = prod.product_ID;
            product = prod;
            carrying_p = true;
            startedGrabbing = false;
            w_parent.UnBlock();
            //w_parent.SetBlockedBy(-1);


        }
        // also done in update()

    }

    private void GrabProduct(Product prod)
    {
        Workstation w_parent = prod.GetComponentInParent<Workstation>();
        if (!prod.IsBlocked())//&& (!w_parent.IsBlocked() || w_parent.BlockedBy()==prod.product_ID)
        {
            w_parent.UnBlock();
            if (!startedGrabbing)
            {
                w_parent.UnBlock();
                //w_parent.SetBlockedBy(ID);
                startedGrabbing = true;
                startingTimeGrabbing= Time.time;
                timeGrabbing = UnityEngine.Random.Range(grabbingTimeLB, grabbingTimeUB);
            }
            else if (Time.time >= startingTimeGrabbing + timeGrabbing)
            {
                BaseStation w_bparent = w_parent as BaseStation;
                if (w_bparent)
                {
                    w_bparent.GiveProduct(prod);
                }
                else
                {
                    w_parent.GiveProduct();
                }
                
                if (!prod.JobInSameMachine())
                {
                    w_parent.UnAssign();
                }

                /*
                BaseStation b_parent = prod.GetComponentInParent<BaseStation>();
                if (!b_parent) {
                    w_parent.UnAssign();
                }
                */

                prod.SetPosition(load.position);
                prod.SetParent(transform);

                task = (int)prod.GetCurrentTask().x;
                prod.grabbed = true;
                prod.LeaveStation();
                grabbedProductID = prod.product_ID;
                product = prod;
                carrying_p = true;
                startedGrabbing = false;
                w_parent.UnBlock();
                //w_parent.SetBlockedBy(-1);
            }

        }
         // also done in update()
        
    }

    private void GrabCombProduct(CombProduct combProd)
    {
        BaseStation b_parent = combProd.GetComponentInParent<BaseStation>();
        if (!combProd.IsBlocked())
        {
            if (!startedGrabbing)
            {
                b_parent.UnBlock();
                //b_parent.SetBlockedBy(ID);
                startedGrabbing = true;
                startingTimeGrabbing = Time.time;
                timeGrabbing = UnityEngine.Random.Range(grabbingTimeLB, grabbingTimeUB);
            }
            else if (Time.time >= startingTimeGrabbing + timeGrabbing)
            {

                if (b_parent)
                {
                    b_parent.GiveCombProduct();
                }
                combProd.SetPosition(load.position);
                combProd.SetParent(transform);

                //
                task = mWaiting;
                combProd.grabbed = true;
                grabbedCombID = combProd.combProduct_ID;
                combProduct = combProd;
                carrying_cp = true;
                startedGrabbing = false;
                b_parent.UnBlock();
                //b_parent.SetBlockedBy(-1);
            }

        }
    }
    private void GrabCombProductold(CombProduct combProd)
    {
        BaseStation b_parent = combProd.GetComponentInParent<BaseStation>();
        if (!combProd.IsBlocked())
        {
            if (b_parent)
            {
                b_parent.GiveCombProduct();
            }
            combProd.SetPosition(load.position);
            combProd.SetParent(transform);

            //
            task = mWaiting;
            combProd.grabbed = true;
            grabbedCombID = combProd.combProduct_ID;
            combProduct = combProd;
            carrying_cp = true;
            startedGrabbing = false;
            b_parent.UnBlock();
            //b_parent.SetBlockedBy(-1);

        }
    }

    private void ResetDecisions()
    {
        //||grabbedCombID != jobComb && combProduct != null && combProduct.grabbed
        //if (grabbedProductID != jobProduct && productDictionary[jobProduct].grabbed && !productDictionary[jobProduct].processing ) //||!carrying_cp && !carrying_p && decided && samePos
        //{
        //    decided = false;
        //}
        //else if(carrying_p && decided && samePos)
        //{
        //    var prod = productDictionary[grabbedProductID];
        //    destination = prod.GetDestination(grabbedProductID, inputLocationDictionary);
        //    destination.y = init_pos.y;
        //    if (transform.position == destination)
        //    {
        //        agent.destination = init_pos;
        //    }
        //    else
        //    {
        //        agent.destination = destination;
        //    }
        //}
        //else if(carrying_cp && decided && samePos)
        //{
        //    Transform temp_w = inputLocationDictionary[mWaiting];
        //    destination = temp_w.position;
        //    destination.y = init_pos.y;
        //    if (transform.position == destination)
        //    {
        //        agent.destination = init_pos;
        //    }
        //    else
        //    {
        //        agent.destination = destination;
        //    }
        //}
    }

    private void DropCombProduct()
    {
        if (!startedDropping)
        {
            startedDropping = true;
            startingTimeDropping = Time.time;
            timeDropping = UnityEngine.Random.Range(droppingTimeLB, droppingTimeUB);
        }
        else if (Time.time >= startingTimeDropping + timeDropping)
        {
            
            /*
            BaseStation b_parent = prod.GetComponentInParent<BaseStation>();
            if (!b_parent) {
                w_parent.UnAssign();
            }
            */
            stations[task].ReceiveCombProduct(combProduct);

            startedDropping = false;
            decided = false;
            combProduct.grabbed = false;
            mWaiting = -1;
            //jobComb = -1;
            carrying_cp = false;
            combProduct = null;

            //w_parent.SetBlockedBy(-1);
        }

    }
    
    private void DropProduct()
    {
        if (!startedDropping)
        {
            stations[task].Block();
            startedDropping = true;
            startingTimeDropping = Time.time;
            timeDropping = UnityEngine.Random.Range(droppingTimeLB, droppingTimeUB);
        }
        else if (Time.time >= startingTimeDropping + timeDropping)
        {
            /*
            BaseStation b_parent = prod.GetComponentInParent<BaseStation>();
            if (!b_parent) {
                w_parent.UnAssign();
            }
            */
            stations[task].ReceiveProduct(product);

            product.grabbed = false;
            product.assigned = false;
            jobProduct = -1;
            carrying_p = false;
            product = null;
            decided = false;
            startedDropping = false;


            //w_parent.SetBlockedBy(-1);
        }


    }
    public bool Decided()
    {
        return decided;
    }

    public bool StartedGrabbing()
    {
        return startedGrabbing;
    }
    public float StartedGrabbingTime()
    {
        return startingTimeGrabbing;
    }
    public float GrabbingDuration()
    {
        return timeGrabbing;
    }
    public bool StartedDropping()
    {
        return startedDropping;
    }
    public float StartedDroppingTime()
    {
        return startingTimeDropping;
    }
    public float DroppingDuration()
    {
        return timeDropping;
    }

}
