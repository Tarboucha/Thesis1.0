using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombProduct : MonoBehaviour
{
    // Start is called before the first frame update
    private static int ID=1;
    public int combProduct_ID=1;
    private Transform og_parent;
    private Workstation parent_station;
    private BaseStation curr_basestation;
    private JSSPMultiAgent curr_agent;
    public float process_time;
    private bool processing = false;
    private bool blocked=false;
    public bool grabbed = false;
    float process_start = 0;
    private Workstation curr_workstation;
    public Color color;
    private Material product_material;
    //private int destinedTo = 0;


    void Awake()
    {
        og_parent = transform.parent;
        BaseStation basestation = og_parent.GetComponent<BaseStation>();
        parent_station = og_parent.GetComponent<Workstation>();
        process_time = UnityEngine.Random.Range(1f, 2f);
        combProduct_ID = CombProduct.ID;
        CombProduct.ID++;
        /*
        if (basestation)
        {
            basestation.ReceiveProduct(transform.GetComponent<Product>());
        }
        */
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        product_material = meshRenderer.material;
        ChangeColor(product_material);
    }

    /*
    public int DestinedTo()
    {
        return destinedTo;
    }
    */
    // Update is called once per frame
    private void Update()
    {
        // Task Completion
        if (processing) { ContinueTask(Time.time); }
    }

    private void ChangeColor(Material material, Color color)
    {
        material.SetColor("_Color", color);
    }

    public void PerformTask(float init_time)
    {
        float time = Time.time;
        //coll.gameObject.SetActive(false); ??

        process_start = time;
        grabbed = false;
        if (time < init_time + process_time)
        {// In case tp > 0
            processing = true;
            Block();
            //ChangeColor(product_material, color_list[0]); à voir plus tard
            SetParent(parent_station.transform);
            SetPosition(parent_station.processing_location.position);
        }
        else
        {// In case of tp = 0
            processing = false;
            SetPosition(parent_station.output_location.position);
            UnBlock();
        }
    }

    /*
    public Vector3 GetDestination(int grabbed_ID, Dictionary<int, Transform> workstationLocations)
    {
        Vector3 destination = new Vector3(0, 0, 0);
        if (!grabbed)
        {
            destination = transform.position;

        }
        else if (grabbed_ID == combProduct_ID)
        {
            int temp_ID = (int)GetCurrentTask()[0]; // get ID of next task
            Transform temp_w = workstationLocations[temp_ID];
            destination = temp_w.position;

        }
        return destination;
    }
    */
    private void ContinueTask(float time)
    {
        //Only runs when the task is completed
        if (time > process_start + process_time)
        {
            processing = false;
            SetPosition(parent_station.output_location.position);
            UnBlock();
        }
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

    public void SetPosition(Vector3 pos)
    {
        transform.position = pos;
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
    public bool IsGrabbed()
    {
        return grabbed;
    }
    public bool IsBeingProcessed()
    {
        return processing;
    }
    
    private void ChangeColor(Material material)
    {
        material.SetColor("_Color", color);
    }
    public void Desactivate()
    {
        transform.gameObject.SetActive(false);
    }
}
