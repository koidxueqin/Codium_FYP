using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shrine : MonoBehaviour
{
    public GameObject ShrineUI;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        ShrineUI.SetActive(true);

    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        ShrineUI.SetActive(false);
    }



    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
