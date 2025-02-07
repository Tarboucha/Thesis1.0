using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseStation : Workstation
{
    // Declare a boolean to track if the product is full or not
    bool full;

    // Reference to the sub-product GameObject
    public GameObject subProduct;

    // Reference to the current combined product
    private CombProduct currComb;

    // Unique base ID for the product
    public int base_ID;

    // Tracks if the product has been assigned
    private bool assigned = false;

    // Stores the nearest capability ID and its distance (default: -1, -1f)
    public (int, float) nearestCapID = (-1, -1f);

    // Flag to indicate if the timer should be reset
    private bool resetTime = false;

    // Flag to block certain operations
    private bool block = false;

    // Indicates if the product is ready to be given
    public bool readyToGive = false;

    // Tracks if the product has sub-products
    private bool haveProds = true;

    // Time required to prepare the product
    private float preparationTime = 5f;

    // Timestamp when preparation starts
    public float preparationStart;

    // Timestamp when preparation finishes
    public float preparationFinished;

    private void Start()
    {
        preparationStart = Time.time;
        ChangeColor(1);
    }

    /// <summary>
    /// Handles the process of giving a product, resetting states, and preparing for the next product/combination product.
    /// </summary>
    public void GiveProduct(Product p)
    {
        //waiting = true;

        outFree = true;
        readyToGive = false;
        preparationStart = Time.time;
        //if (processing && !processing.processing)
        //{
        //    //blockedBy= processing.product_ID;
        //    readyProd = processing;
        //    processing = null;
        //    readyProd.SetPosition(output_location.position);
        //    readyProd.UnBlock();
        //    blocked = false;
        //}
        //else
        //{
        //    blocked = false;
        //    readyProd = null;
        //}
    }


    /// <summary>
    /// Receives a product and prepares it for processing.
    /// This method sets the product's parent, blocks it from further interaction,
    /// and moves it to the designated processing location.
    /// Additional logic for handling combinations or tasks can be uncommented if needed.
    /// </summary>
    /// <param name="p">The product to be received and processed.</param>

    public override void ReceiveProduct(Product p)
    {
        //block=true;
        //outFree = true;
        p.SetParent(transform);
        p.Block();
        p.SetPosition(processing_location.position);
        //if (controller.GetRequiredCombDict().ContainsKey(t))
        //{
        //    expectedComb = controller.GetRequiredCombDict()[t];
        //}
        //waitingProd = p;
        //p.PerformTask(Time.time, transform);
    }

    public void Finish(int product_ID)
    {
        controller.GetProducts()[product_ID].SetPosition(output_location.position);
    }

    public bool IsAssigned()
    {
        return assigned;
    }
    public void Assign(bool b)
    {
        assigned = b;
    }
    private void Update()
    {
        // Check if the station has a Product/Combination Product
        if (haveProds)
        {
            Product[] childProducts = GetComponentsInChildren<Product>();
            // If no child products are found, update the state
            if (childProducts.Length == 0)
            {
                haveProds = false;
            }
            else
            {
                foreach (var prod in childProducts)
                {
                    // Check if the product's task pointer is at the starting state and if the starting time has elapsed
                    if (prod.task_pointer == 0 && prod.starting_time < Time.time - controller.getEpisodeStart())
                    {
                        prod.blocked=false;
                        prod.task_pointer = 1;
                        prod.SetPosition(output_location.position);
                    }
                }
            }
        }
        // Check if the product is not ready to be given and if the preparation time has elapsed
        if (!readyToGive && preparationStart + preparationTime < Time.time)
        {
            // Mark the preparation as finished and set a new random preparation time
            preparationFinished = Time.time - controller.getEpisodeStart();
            preparationTime = UnityEngine.Random.Range(4f, 7f);
            // Set the product as ready to be given
            readyToGive = true;
        }
        // Check if there is no current combined product and if the reset time flag is not set
        if (currComb==null && !resetTime)
        {
            // Instantiate a new combined product
            InstantiateCombProduct();
        }
    }
    /*
    public void ReceiveCombProduct(CombProduct combProd)
    {
        combProd.SetParent(transform);
        currComb = combProd;
    }
    */
    // returns the time in which the station would be ready to provide a base element
    public float ReadyTime()
    {
        if (readyToGive)
        {
            return preparationFinished;
        }
        return preparationStart - controller.getEpisodeStart() + preparationTime;
    }

    // Instantiates a base element that serves as a combination/payment element.
    // The instantiated object is placed at the output location and assigned as the current combination product.
    public void InstantiateCombProduct()
    {
        Instantiate(subProduct, GetOutputLocation().Item1, Quaternion.identity, transform);
        currComb = transform.GetComponentInChildren<CombProduct>();
    }

    // Returns the current combination product instance.
    public CombProduct GetCurrCombProd()
    {
        return currComb;
    }
    public void GiveCombProduct()
    {
        assigned = false;
        outFree = true;
        readyToGive = false;
        preparationStart = Time.time;
        InstantiateCombProduct();                                         
    }
    public void TimeToReset()
    {
        resetTime = true;
    }

    public override bool IsBlocked()
    {
        return block;
    }
    public override void Block()
    {
        block = true;
    }
    public override void UnBlock()
    {
        block = false;
    }

}
