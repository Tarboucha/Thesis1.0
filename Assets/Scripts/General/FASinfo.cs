using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FASinfo : MonoBehaviour
{
    // Color List for Products and Workstations
    [Tooltip("Colors for Products and Workstations")]
    public List<Color> color_list;
    public List<Color> bs_colors;
    public List<Color> rs_colors;
    public Transform Origin;
    public int Nw;
    public bool randomJob;
    public int jobSeed=-1;
    public int startingInst = 0;
    
    public bool seedIncr = false;
    //public int machineSeed;
    public int n_products;
    public bool printMakespsan = false;
    public int instanceNum = 0;
    public bool randomness = false;

    


    public float grabbingTimeUB = 15f;
    public float grabbingTimeLB = 14f;
    public float droppingTimeUB = 15f;
    public float droppingTimeLB = 14f;

    public float maxEpisodeTime = 1200f;

    public float agent_speed = 1f;

    private int basePort;

    public void incrSeed()
    {
        jobSeed += 1;
    }
    public void setSeed(int s)
    {
        jobSeed =s;
    }


    public void Awake()
    {
        //string[] args = System.Environment.GetCommandLineArgs();
        //for (int i = 0; i < args.Length; i++)
        //{
        //    if (args[i] == "--base-port" && i + 1 < args.Length)
        //    {
        //        if (int.TryParse(args[i + 1], out bool base_port))
        //        {
        //            basePort = base_port;
        //        }
        //        else
        //        {
        //            Debug.Log("Invalid base port value: " + args[i + 1]);
        //        }
        //    }

        //}

    }
}