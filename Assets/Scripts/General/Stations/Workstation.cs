using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Workstation : MonoBehaviour
{
    [Tooltip("Workstation ID. 0 means it is not working")]
    public int ID;

    public static int station_ID=1;

    [Tooltip("Stack output products")]
    public bool stack_output;

    public List<int> jobsID;

    // Color List for FAS Visualization
    private Material product_material;

    public MultiAgentController controller;

    // Locations and Colliders
    public Transform input_location;
    public Transform output_location;
    public Transform processing_location;
    public Collider input_collider;
    //public bool willHave = false;

    public bool inFree = true;
    public bool outFree = true;

    // product in the ready position
    protected Product readyProd = null;

    // product in waiting position
    protected Product waitingProd = null;

    // product in processing position
    protected Product processing = null;

    // Is an AGV assigned to transport and element/product to this station?
    private bool waiting = true;

    // Number of payments/combination elements the station is waiting for 
    private int expectedComb = 0;

    // Number of payments/combination elements the station already received
    private int receivedComb = 0;

    // Number of payments/combination elements are currently being transported to this station
    private int willReceive = 0;

    // is the station currently blocked.
    protected bool blocked = false;
    //private int blockedBy = -1;
    
    private void Awake()
    {
        ID = Workstation.station_ID;
        Workstation.station_ID++;
        if (Workstation.station_ID == 7)
        {
            Workstation.station_ID = 1;
        }
        controller = transform.parent.GetComponentInParent<MultiAgentController>();
    }

    // number of product currently contained in the station
    public int nProds()
    {
        int n = 0;
        if (waitingProd)
        {
            n += 1;
        }
        if (processing)
        {
            n += 1;
        }
        if (readyProd)
        {
            n += 1;
        }
        return n;
    }
    /*
    private void Update()
    {
        if (expectedComb != receivedComb)
        {

        }
    }
    */
    public bool IsWaiting()
    {
        return waiting;
    }
    public void Assign()
    {
        waiting = false;
    }
    public void UnAssign()
    {
        waiting = true;
    }

    public void AssignInTask(int task)
    {
        Assign();
        inFree = false;
        if (controller.GetRequiredCombDict().ContainsKey(task))
        {
            expectedComb = controller.GetRequiredCombDict()[task];
        }
    }

    public void AssignOutTask()
    {
        outFree = false;
    }

    // Is the station waiting for payments/combination elements?
    public bool waitingForComb()
    {
        if (receivedComb + willReceive < expectedComb)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    // indicate to the station is going to receive
    public void WillReceive(int n)
    {
        willReceive += n;
        inFree = false;
    }

    public void WillNotReceive(int n)
    {
        willReceive -= n;
    }

    // Handles the logic for the station receiving a combination/payment product.
    // Updates the station's state, processes the product, and starts task execution if conditions are met.
    public void ReceiveCombProduct(CombProduct combProd)
    {
        combProd.SetParent(transform);
        combProd.gameObject.SetActive(false);
        inFree = true;

        willReceive--;
        receivedComb++;
        if (waitingProd && expectedComb != 0 && receivedComb >= expectedComb)
        {
            //controller.DesactivateWaiting(ID);
            receivedComb = 0;
            expectedComb = 0;
            //Product p = transform.GetComponentInChildren<Product>();
            if (!processing)
            {
                
                processing = waitingProd;
                waitingProd = null;
                processing.PerformTask(Time.time, transform);
            }
        }
    }


    // Handles the logic for the station receiving a product.
    // Updates the station's state, blocks the product, and starts processing if conditions are met.
    public virtual void ReceiveProduct(Product p)
    {
        // Set the product's parent to the station and mark the input as free.
        p.SetParent(transform);
        inFree = true;

        // Block the station and the product, and set the product's position to the input location.
        blocked = true;
        p.Block();
        p.SetPosition(input_location.position);
        p.SetStation(this);

        // Check if the station is already processing another product.
        if (processing)
        {
            // If the station is busy, add the product to the waiting queue.
            waitingProd = p;
        }
        else
        {
            // If no product is being processed, check if the combination requirements are met.
            if (expectedComb == 0 || receivedComb >= expectedComb)
            {
                // Set the product's position to the input location and start processing.
                p.SetPosition(input_location.position);
                processing = p;
                receivedComb = 0;
                expectedComb = 0;

                // Perform the task associated with the product.
                p.PerformTask(Time.time, transform);
            }
            else
            {
                // If combination requirements are not met, add the product to the waiting queue.
                waitingProd = p;
            }
        }
    }



    // Handles the logic for giving a product from the station.
    // Updates the state of the station, including the processing and ready product, and unblocks the station if necessary.
    public virtual void GiveProduct()
    {
        //waiting = true;
        outFree = true;
        readyProd.LeaveStation();
        if (processing && !processing.processing)
        {
            //blockedBy= processing.product_ID;
            readyProd = processing;
            processing = null;
            readyProd.SetPosition(output_location.position);
            readyProd.UnBlock();
            blocked = false;
            //if(waitingProd)
            //if (expectedComb == 0 || receivedComb >= expectedComb) { 
            //    receivedComb = 0;
            //    expectedComb = 0;
            //    processing.PerformTask(Time.time, transform);
            //    processing = waitingProd;
            //    waitingProd = null;
            //}
            //blocked = true;
        }
        //else if (processing)
        //{
        //    blockedBy = processing.product_ID;
        //    readyProd = null;
        //    blocked = true;
        //}
        else
        {
            //blockedBy = -1;
            blocked = false;
            //processing = null;
            readyProd = null;
        }
    }
    
    // Handles the logic for when the current product finishes processing.
    // Updates the station's state, moves the product to the output location, and starts processing the next waiting product if conditions are met.
    public void CurrProductFinish()
    {
        if (!readyProd)
        {
            blocked = false;
            readyProd = processing;
            processing = null;
            readyProd.SetPosition(output_location.position);
            readyProd.UnBlock();
        }
        if (waitingProd)
        {
            //blockedBy = waitingProd.product_ID;
            if (expectedComb == 0 || receivedComb >= expectedComb)
            {
                processing = waitingProd;
                //inFree = true;
                waitingProd = null;
                processing.SetPosition(input_location.position);
                receivedComb = 0;
                expectedComb = 0;
                processing.PerformTask(Time.time, transform);
            }
        }
    }

    public void ChangeColor(int num) {
        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();

        product_material = meshRenderer.material;

        product_material.SetColor("_Color", controller.GetColor(num));

    }

    // Function to get output location of processed products

    public Vector3 GetProcessLocation()
    {
        return processing_location.position;
    }

    public (Vector3,int) GetOutputLocation()
    {
        Vector3 pos = output_location.position;
        List<int> placementIDs = new List<int>();

        if (stack_output) { 
            return (pos, 0); 
        }

        int c = 0;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.CompareTag("product"))
            {
                c++;

                placementIDs.Add(child.GetComponent<Product>().workstationPlacementID);
            } 
        }

        while (placementIDs.Contains(c)){c++;}

        c--;
        switch (c)
        {
            case 1:
                pos.z += 0.3f;
                break;
            case 2:
                pos.z -= 0.3f;
                break;
            case 3:
                pos.x += 0.3f;
                break;
            case 4:
                pos.x += 0.3f;
                pos.z += 0.3f;
                break;
            case 5:
                pos.x += 0.3f;
                pos.z -= 0.3f;
                break;
        }
        return (pos, c);
    }

    // Reset Workstation per episode
    public void EpisodeReset()
    {
        input_collider.gameObject.SetActive(true);
    }

    public Product GetNextProd()
    {
        if (readyProd)
        {
            return readyProd;
        }
        else if (processing)
        {
            return processing;
        }
        else if (waitingProd)
        {
            return waitingProd;
        }
        return null;
    }
    public Product GetProcessing()
    {
        return processing;
    }
    public Product GetReadyProd()
    {
        return readyProd;
    }
    public Product GetWaitingProd()
    {
        return waitingProd;
    }
    public bool IsAvailable()
    {
        if (waitingProd!=null || (processing!=null && readyProd!=null))
        {
            return false;
        }
        return true;
    }
    public void Desactivate()
    {
        transform.gameObject.SetActive(false);
    }

    public virtual bool IsBlocked()
    {
        return blocked;
    }
    public virtual void Block()
    {
        blocked = true;
    }
    public virtual void UnBlock()
    {
        blocked = false;
    }

    //public int BlockedBy()
    //{
    //    return blockedBy;
    //}
    //public void SetBlockedBy(int avg_ID)
    //{
    //    blockedBy = avg_ID;
    //}
    public bool WaitingAvailable()
    {
        if (waitingProd)
        {
            return false;
        }
        return true;
    }

    public int ExpectingCombs()
    {
        return expectedComb - (receivedComb + willReceive);
    }
}
