using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Starship : MonoBehaviour
{
    public int engine_amount = 3;
    public GameObject departure_object;
    public GameObject destination_object;
    public GameObject engine_front;
    public GameObject engine_back;
    public GameObject engine_left;
    public GameObject engine_right;
    public Engine_Starship[] engines;
    public KeyCode[] key_engine;


    private Vector3 departure;
    private Vector3 destination;
    private Vector3[] observation;

    private Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = gameObject.GetComponent<Rigidbody>();
        departure_object = GameObject.Find("°±¾÷©W1");
        destination_object = GameObject.Find("°±¾÷©W2");
        engines = new Engine_Starship[] { engine_front.GetComponent<Engine_front>(),
                                          engine_back.GetComponent<Engine_back>(),
                                          engine_left.GetComponent<Engine_left>(),
                                          engine_right.GetComponent<Engine_right>()};

        key_engine = new KeyCode[] { KeyCode.W,
                                     KeyCode.S,
                                     KeyCode.A, 
                                     KeyCode.D};

        for (int i = 0; i < engines.Length; i++)
        {
        }
    }

    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < engines.Length; i++)
        {
            if (Input.GetKey(key_engine[i]) && !(engines[i].started))
            {
                engines[i].launch();
            }
            else if (!Input.GetKey(key_engine[i]) && engines[i].started)
            {
                engines[i].shut_down();
            }
        }
        /*
        observation[0] = this.transform.position;
        observation[1] = this.transform.rotation.eulerAngles;
        observation[2] = departure_object.transform.position;
        observation[3] = destination_object.transform.position;
        */
    }
}

public class Engine_Starship : MonoBehaviour
{
    public bool started;
    public float thrust;
    public Vector3 rotation_temp;

    public Rigidbody rb;

    public void Start()
    {
        started = false;
        thrust = 20f;

        rb = gameObject.GetComponent<Rigidbody>();
    }

    public void Update()
    {
        if (started)
        {
            rb.AddRelativeForce(new Vector3(0, thrust, 0));
        }
    }

    public void launch()
    {
        started = true;
    }
    public void shut_down()
    {
        started = false;
    }
    public void set_power(float amount)
    {
        thrust = amount;
    }
    public void adjust_power(float amount)
    {
        thrust += amount;
    }
    public void set_rotation(Vector3 amount)
    {
        this.transform.Rotate(amount);
    }
    public void adjust_rotation(Vector3 amount)
    {
        this.transform.rotation = Quaternion.Euler(new Vector3(this.transform.rotation.x + amount.x, this.transform.rotation.y + amount.y, this.transform.rotation.z + amount.z));
    }
}
