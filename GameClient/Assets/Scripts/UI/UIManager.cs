﻿using TMPro;
using UnityEngine;

/// <summary>
/// Contains all methods to control and update the in-game user interface
/// </summary>
public class UIManager : MonoBehaviour
{
#pragma warning disable 0649
    // Canvas
    [SerializeField]
    private GameObject HUD, Menu;
    private bool menuIsOpen = false;

    // Frames
    [SerializeField]
    private GameObject PlayersFrame, WinnerFrame, LoserFrame;

    // Qualified number text
    [SerializeField]
    private TMP_Text qualifedNum;

    // Qualified feed
    [SerializeField]
    private Transform qualifiedFeedParent;
    [SerializeField]
    private GameObject qualifiedItem;

    // Disconnected Window
    [SerializeField]
    private GameObject disconMenuParent;
#pragma warning restore 0649

    /// <summary>Setup all ui elements.</summary>
    public void Initialize()
    {
        HUD.SetActive(true);
        Menu.SetActive(false);

        PlayersFrame.SetActive(true);
        WinnerFrame.SetActive(false);
        LoserFrame.SetActive(false);
    }

    /// <summary>Activates/Deactivates the menu.</summary>
    public void OpenMenu()
    {
        menuIsOpen = !menuIsOpen;

        if (!menuIsOpen)
        {
            Menu.transform.GetChild(1).gameObject.SetActive(true);
            Menu.transform.GetChild(2).gameObject.SetActive(false);
        }

        Menu.SetActive(menuIsOpen);
    }

    /// <summary> Used for updating the qualified number of players and the total of them </summary>
    public void UpdateQualifiedNum(int _qualified, int _total)
    {
        // Update qualified number text
        qualifedNum.text = $"{_qualified}/{_total}";
    }

    /// <summary>Activates the winner frame element if player qualified in the race.</summary>
    public void Qualified()
    {
        WinnerFrame.SetActive(true);
    }

    /// <summary>Activates the loser frame element if player did not qualify in the race.</summary>
    public void UnQualified()
    {
        LoserFrame.SetActive(true);
    }

    /// <summary>Updates the qualified number of players and displays the player on the qualified feed.</summary>
    /// <param name="_qualified">The number of qualified players.</param>
    /// <param name="_total">The total players in the race.</param>
    /// <param name="_username">The player's username that qualified.</param>
    public void OnQualified(int _qualified, int _total, string _username)
    {
        UpdateQualifiedNum(_qualified, _total);

        // Add event to qualified feed
        GameObject item = Instantiate(qualifiedItem, qualifiedFeedParent);
        // Setup display information
        item.transform.GetChild(0).GetComponent<TMP_Text>().text = _username;
        // Destroy object after x seconds
        Destroy(item, 4f);
    }

    public void DisconnectedMenu()
    {
        disconMenuParent.SetActive(true);
    }

    public void ExitGame()
    {
        GameManager.instance.LeaveRoom();
    }
}
