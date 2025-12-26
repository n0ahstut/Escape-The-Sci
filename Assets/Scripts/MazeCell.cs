using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeCell : MonoBehaviour
{
    [SerializeField]
    public GameObject _leftWall;
    [SerializeField]
    public GameObject _rightWall;
    [SerializeField]
    public GameObject _frontWall;
    [SerializeField]
    public GameObject _backWall;
    [SerializeField]
    public GameObject _unvisitedBlock;

    public bool IsVisited { get; private set; }

    public void Visit()
    {
        IsVisited = true;
        _unvisitedBlock.SetActive(false);

    }
    public void ClearLeftWall()
    {
        _leftWall.SetActive(false);
    }
    public void ClearRightWall()
    {
        _rightWall.SetActive(false);
    }
    public void ClearFrontWall()
    {
        _frontWall.SetActive(false);
    }
    public void ClearBackWall()
    {
        _backWall.SetActive(false);
    }
    public bool HasRightWall()
    {
        return _rightWall != null && _rightWall.activeInHierarchy;
    }

    public bool HasLeftWall()
    {
        return _leftWall != null && _leftWall.activeInHierarchy;
    }

    public bool HasFrontWall()
    {
        return _frontWall != null && _frontWall.activeInHierarchy;
    }

    public bool HasBackWall()
    {
        return _backWall != null && _backWall.activeInHierarchy;
    }
}