using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterChooser : MonoBehaviour
{

    [SerializeField] private FrameReader frameReader;
    [SerializeField] private SlideShow slideShow;
    [SerializeField] public int commandLineCharacter = -1;

    private void Start()
    {
        slideShow.onSelection += OnCharacterSelect;
        slideShow.SetCharacterIndex(commandLineCharacter);
    }

    private void OnCharacterSelect(int index,GameObject node)
    {
        frameReader.SetNewCharacter(Instantiate(node));
        UIManager.Instancce.OnSlideShowExit(this.gameObject);
    }

    public void setCommandLineCharacterIndex(int cmdIndex) { 
        this.commandLineCharacter = cmdIndex;
    }
}
