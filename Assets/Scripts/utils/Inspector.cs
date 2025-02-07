using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Inspector script to test FAS
public class Inspector : MonoBehaviour
{
    GameObject gameobject;

    new private Rigidbody rigidbody;

    public float speed = 2f;
    public Transform load;

    private Product product;
    private bool carrying;
    private Vector2 task;

    Workstation workstation;
    private Dictionary<int, Transform> locationDictionary;
    private Dictionary<Collider, Workstation> workstationColliderDictionary;


    private void Awake()
    {
        carrying = false;
        gameobject = transform.gameObject;
        rigidbody = GetComponent<Rigidbody>();
        locationDictionary = new Dictionary<int, Transform>();
        workstationColliderDictionary = new Dictionary<Collider, Workstation>();
        GetLocations(transform.parent);
        Debug.Log("ESto corre");
    }


    // Update is called once per frame
    void Update()
    {
        Vector3 forward = Vector3.zero;
        Vector3 right = Vector3.zero;

        // Forward/backward
        if (Input.GetKey(KeyCode.W))
        {
            forward = transform.forward * speed;
        }
        if (Input.GetKey(KeyCode.S))
        {
            forward -= transform.forward * speed;
        }

        // Left/right
        if (Input.GetKey(KeyCode.A))
        {
            right = -transform.right * speed;
        }
        if (Input.GetKey(KeyCode.D))
        {
            right += transform.right * speed;
        }

        Vector3 combined = (forward + right);
        rigidbody.velocity = combined;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collider collider = collision.collider;
        if (!carrying)
        {
            if (collider.CompareTag("product"))
            {
                product = collider.gameObject.GetComponent<Product>();
                GrabProduct(product);

            }
            if (collider.CompareTag("table"))
            {
                TemporalStation temp = collider.transform.GetComponent<TemporalStation>();
                if (temp.full)
                {
                    product = temp.GetProduct();
                    GrabProduct(product);
                    temp.full = false;
                    carrying = true;
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
                    carrying = false;
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
        if (carrying)
        {
            // Colliding with Workstations
            if (task.x == workstationColliderDictionary[collider].ID)
            {
                //AddReward(2.5f);
                //collider.gameObject.SetActive(false); // Disables Workstation
                workstationColliderDictionary[collider].ReceiveProduct(product);
                //decided = false;

            }
            /*
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
        }
        /*
        else
        {
            if (collider.CompareTag("product"))
            {
                product = collider.gameObject.GetComponent<Product>();
                if (product.product_ID == job)
                {
                    GrabProduct(product);
                }
            }
        }
        */

    }

    private void GetLocations(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            // Get Workstation Locations
            Transform child = parent.GetChild(i);

            if(child.CompareTag("basestation") || child.CompareTag("ringstation") || child.CompareTag("capstation") || child.CompareTag("deliverystation"))
            {
                workstation = child.gameObject.GetComponent<Workstation>();
                Transform input = workstation.input_location;
                Collider coll = workstation.input_collider;

                //Debug.Log("Workstation Read: "+workstation.workstation_ID);
                locationDictionary.Add(workstation.ID, input);
                workstationColliderDictionary.Add(coll, workstation);
            }
        }
    }

    private void GrabProduct(Product product)
    {

        product.SetPosition(load.position);
        carrying = true;
        product.SetParent(transform);
        task = product.GetCurrentTask();

    }
}
