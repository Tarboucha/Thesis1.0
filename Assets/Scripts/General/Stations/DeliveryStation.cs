using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeliveryStation : Workstation
{
    private void Start()
    {
        // Set Workstation Color
        ChangeColor(4);
    }

    public void ProductFinished()
    {
        controller.ProductFinished();
    }
}
