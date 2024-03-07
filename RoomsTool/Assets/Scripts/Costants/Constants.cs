using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Constants
{
    public const string TOOL_NAME = "Tools/RoomsTool";
    public const string FOLDER_PATH = "Assets/Prefabs/Rooms";
    public const string TEXTURE_PATH = "Assets/Materials";

    public const string BASE_ORDER_STRING = "N of Doors: ";

    public const string EXPLANATION = "\nSelect the layer of the terrain on which you want to positionate the rooms. \nThe rooms can only be positionated " +
        "in positions that fits the room in its width and depth.\n\nCOMMANDS:\n\nSPACE BAR -> Positionate room in selected position.\nSHIFT+SCROLL_WHEEL -> Rotate the " +
        "room by 90°, clockwise or not.\nSHIFT+A/W/S/D -> Navigate through folders (the selected room will be kept the same \nwhile navigating unless another room is " +
        "selected).";

    public const float ACCEPTANCE = 0.3f;

    public static Vector3 Round(this Vector3 v) 
    {
        return new(Mathf.Round(v.x),
                   Mathf.Round(v.y),
                   Mathf.Round(v.z));
    }
}
