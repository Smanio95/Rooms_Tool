using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    [SerializeField] Transform floor;
    public Transform[] entrances;
    public int NumOfEntraces { get => entrances.Length; }
    public float width { get => floor ? floor.localScale.x : -1; }
    public float depth { get => floor ? floor.localScale.z : -1; }
}
