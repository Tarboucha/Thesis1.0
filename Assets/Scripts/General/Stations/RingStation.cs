using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RingStation : Workstation
{
    public int ring_ID = -1;
    public (int, float) nearestCapID = (-1,-1f);
    //public float disToBase = 0f;

    private void Start()
    {
        // Set Workstation Color
        ChangeColor(2);
    }

}
