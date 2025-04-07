using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public float horizontal;
    public float vertical;
    public NetworkButtons buttons;
}