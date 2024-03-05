using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    public Transform[] entrances;
    public int NumOfEntraces { get => entrances.Length; }
}
