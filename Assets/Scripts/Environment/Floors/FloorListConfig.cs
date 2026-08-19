using UnityEngine;
using System.Collections.Generic;

public class FloorListConfig : ScriptableObject
{
    public Floor[] floors;
    public bool prevenConsecutiveDuplicates = true;
    private int lastSelectedIndex = -1;

    public Floor GetRandomFloor()
    {
        if (floors == null || floors.Length == 0)
            return null;

        int index;
        if (prevenConsecutiveDuplicates)
        {
            do
            {
                index = Random.Range(0, floors.Length);
            } while (index == lastSelectedIndex && floors.Length > 1);
            lastSelectedIndex = index;
        }
        else
        {
            index = Random.Range(0, floors.Length);
        }
        return floors[index];
    }

    public Floor GetFloorByName(string floorName)
    {
        if (floors == null || floors.Length == 0)
            return null;

        foreach (var floor in floors)
        {
            if (floor.floorName == floorName)
                return floor;
        }
        return null;
    }

}