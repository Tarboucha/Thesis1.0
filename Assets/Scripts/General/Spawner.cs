using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{

    private Dictionary<int, BaseStation> baseStationDictionary;
    private Dictionary<int, Product> productDictionary;
    private Transform og_parent;
    private Workstation parent_workstation;
    private Vector3 og_position;
    private Vector3 output_position;
    private Product currProduct;
    private List<Transform> childs;

    [Tooltip("Ordered list of tasks to complete job. x: task, y: processing time. Add delivery station at the end with a negative ID at x")]
    private List<Vector3> job;


    private void Awake()
    {
        output_position = new Vector3();
        og_parent = transform.parent;
        parent_workstation = transform.parent.GetComponent<Workstation>();
        og_position = transform.position;
        productDictionary = new Dictionary<int, Product>();
        childs = new List<Transform>();
        //output_position=og_parent.output.position
        //episodeStart = Time.time;
        //finished = false;
        //origin = info.Origin.position;
        //MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        //product_material = meshRenderer.material;

        //ogTimeScale = Time.timeScale;
    }
    // Start is called before the first frame update
    void Start()
    {
        Collectchildren();
        SpawnObject();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SpawnObject()
    {
        for (int i=0; productDictionary.Count < i; i++)
        {
            if (productDictionary[i].job[0].x == parent_workstation.ID)
            {
                Instantiate(currProduct, og_parent.position, og_parent.rotation);
            }
        }

    }

    public void Collectchildren()
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
            /*
            else if (child.CompareTag("AGV"))
            {
                childs.Add(child);
                JSSPMultiAgent agent = child.GetComponent<JSSPMultiAgent>();
                AGVgroup.RegisterAgent(agent);
                agents.Add(agent);
                agent.agent.speed = speed;
            }
            */
        }
    }

}
