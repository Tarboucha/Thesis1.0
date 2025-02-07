using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CSVManager : MonoBehaviour
{
    private string filePath= "D:\\111_Work\\MA2\\Logs\\CSV";

    private void Start()
    {
        // Set the file path for the CSV file
        filePath = Application.persistentDataPath + "/RunningInfos.csv";

        // Create the CSV file if it doesn't exist
        if (!File.Exists(filePath))
        {
            CreateCSVFile();
        }
    }

    private void CreateCSVFile()
    {
        // Add headers
        string[] headers = new string[] { "Instance ID", "Reward", "Cumulative Reward", "Time Elapsed (s)" };

        // Write headers to file
        File.WriteAllText(filePath, string.Join(",", headers) + "\n");
    }

    public void SaveRunningInfo(int instanceId, float reward, float cumReward, float timeElapsed)
    {
        // Create a new line of data
        string[] data = new string[] { instanceId.ToString(), reward.ToString(), cumReward.ToString(), timeElapsed.ToString() };

        // Append data to file
        File.AppendAllText(filePath, string.Join(",", data) + "\n");
    }
}
