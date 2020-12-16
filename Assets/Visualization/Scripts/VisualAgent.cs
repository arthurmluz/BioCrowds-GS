
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisualAgent : MonoBehaviour 
{
    private Animator anim;
    [SerializeField]
    public Queue<float> moveMem;
    public float[] qview;
    private Vector3 currPosition;
    private bool updated;
    private bool initialized;
    private Vector3 currMoveVect;
    private Vector3 prevMoveVect;

    void Start()
    {
        if (!initialized) {
            Initialize(transform.parent.position);
        }
    }
	// Update is called once per frame
	void Update () 
    {
        prevMoveVect = currMoveVect;
        currMoveVect = currPosition - transform.parent.position;
        currMoveVect.y = 0f;
        //Debug.Log(currMoveVect.x + " " + currMoveVect.z);
        moveMem.Dequeue();
        //moveMem.Enqueue(currMoveVect);
        moveMem.Enqueue(currMoveVect.magnitude);
        float speedSum = 0;
        //float angleDifSum = 0;
        var prevV = moveMem.Peek();
        foreach(float v in moveMem){
            speedSum += v;
            //angleDifSum += Vector3.SignedAngle(prevV, v,Vector3.back);
            prevV = v;
        }
        float presentAvgSpeed = (speedSum  / moveMem.Count) ;
        float estFutureSpeed = currMoveVect.magnitude;
        float AvgSpeed = (presentAvgSpeed + estFutureSpeed) / 2;

        //float presentAvgAngleDif = angleDifSum / moveMem.Count;
        //float estFutureAngDif = Vector3.SignedAngle(prevV, currMoveVect, Vector3.back);
        //float avgAngleDif = (presentAvgAngleDif + estFutureAngDif) / 2;
        float totalAngleDiff = Vector3.SignedAngle(currMoveVect, prevMoveVect, Vector3.up);
        //Debug.Log(totalAngleDiff);
        float angFact = totalAngleDiff / 90f;
        anim.SetFloat("AngSpeed", angFact * 0.5f);// Mathf.Clamp(angDif/6f,-1f,1f));

        
        //transform.Rotate(new Vector3(0, totalAngleDiff * 0.05f, 0), Space.World);
        //transform.rotation = Quaternion.Euler(0, Mathf.Atan2(speed.x,speed.z)*180f,0);
        transform.LookAt(transform.position - currMoveVect, Vector3.up);
        anim.SetFloat("Speed", Mathf.Clamp(presentAvgSpeed*6f, 0f, 0.9f));
        //anim.SetFloat("AngSpeed", presentAvgAngleDif/3f);
        //anim.SetFloat("Motion_Time", anim.GetFloat("Motion_Time") + 0.02f);
        //transform.position = currPosition;
        currPosition = transform.parent.position;
        qview = moveMem.ToArray();
        updated = false;

    }

    public void Initialize(Vector3 pos)
    {
        //transform.Rotate(Vector3.right,-90) ;
        anim = GetComponent<Animator>();
        moveMem = new Queue<float>();
        currPosition = new Vector3(pos.x, pos.y, pos.z);
        transform.position = currPosition;
        updated = false;
        for (int i = 0; i < 15; i++)
        {
            moveMem.Enqueue(0);
        }

        initialized = true;
    }



}

