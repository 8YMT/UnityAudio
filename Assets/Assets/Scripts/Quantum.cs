using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Quantum: MonoBehaviour
{
    public AudioSource audioSource;

    private AudioClip audioClip;
    private float[] samples;

    //Right and left Channels
    private float[] leftsamples;
    private float[] rightsamples;

    //samples length
    private int allsamplesLength;
    private int singlechannelsamplesLength;

    //blocking
    private int blocksize = 1024;
    private float[,] leftblockedsamples;
    private float[,] rightblockedsamples;
    private int blockcount;
    
    //averaging blocks
    private float[] averageBlockLeft;
    private float[] averageBlockRight;

    //Block Energy
    private float[] RightaverageBlockEnergy;
    private float[] LeftaverageBlockEnergy;

    //RMS
    private float[] RMSright;
    private float[] RMSleft;
    

    //RMS SLIDING Momentary
    private float[] RMSSlidingright;
    private float[] RMSSlidingleft;
    private int windowsize=18;
    private int slide;

    //progression over time
    [SerializeField][Range(-1, 1)] private float magnitude;
    [SerializeField][Range(0, 10)] private float energy = 0;







    void Start(){
        //getting the audio clip
        audioClip = audioSource.clip;
        
        //getting length 
        allsamplesLength = audioClip.channels * audioClip.samples;
        singlechannelsamplesLength = audioClip.samples;
        if (singlechannelsamplesLength % blocksize != 0)
            blockcount = (singlechannelsamplesLength / blocksize) + 1;
        else
            blockcount = singlechannelsamplesLength / blocksize;
        
        //allocations
        samples = new float[allsamplesLength];
        leftsamples = new float[singlechannelsamplesLength];
        rightsamples = new float[singlechannelsamplesLength];
        leftblockedsamples = new float[blockcount, blocksize];
        rightblockedsamples = new float[blockcount, blocksize];
        averageBlockLeft = new float[blockcount];
        averageBlockRight = new float[blockcount];
        RightaverageBlockEnergy = new float[blockcount];
        LeftaverageBlockEnergy = new float[blockcount];
        RMSright = new float[blockcount];
        RMSleft = new float[blockcount];
        slide = blockcount - windowsize + 1;
        RMSSlidingright = new float[slide];
        RMSSlidingleft = new float[slide];



        //filling samples with audio clip samples 
        audioClip.GetData(samples, 0);

        //filling channels on their own
        RightChannel(); // right channel samples - rightsamples
        LeftChannel(); //  left channel samples - leftsamples

        // blocking channels
        RightChannelBlock(); // right blocked channel samples - rightblockedsamples
        LeftChannelBlock(); // left blocked channel samples - left blockedsamples

        //averaging blocks
        AverageBlockRight(); // right averaged blocks - averageBlockRight
        AverageBlockLeft(); // left averaged blocks - averageBlockLeft

        // EnergyBlocks
        EnergyRight(); // Righ Energy - RightaverageBlockEnergy
        EnergyLeft(); // Left Energy - LeftaverageBlockEnergy

        //RMS
        RMSRight(); //right Rms - RMSright
        RMSLeft(); // left RMS - RMSleft

        //RMS Sliding
        RMSSlidingRight(); // right Momentary sliding RMS - RMSSlidingright
        RMSSlidingLeft(); // left Momentary sliding RMS - RLSSlidingleft

        Debug.Log("Check Audio Object For Energy and Mangitude Values");

    }



    void Update(){
        magnitude = rightsamples[audioSource.timeSamples/2];
        energy = RightaverageBlockEnergy[(audioSource.timeSamples / blocksize)] * 10f;
    }



//Right Process

    void RightChannel()
    {
        int y=0;
        for (int i = 1; i < allsamplesLength; i = i + 2)
        {
            rightsamples[y] = samples[i];
            y++;
        }
    }

    void RightChannelBlock(){
        int z = 0;
        for (int i = 0; i < blockcount; i++)
        {
            for (int y= 0; y < blocksize; y++)
            {
                if (z == (singlechannelsamplesLength - 1))
                    break;
                rightblockedsamples[i, y] = rightsamples[z];
                z++;
            }
        }
    }
    void AverageBlockRight()
    {
        float sum = 0;
        for (int i = 0; i < blockcount; i++)
        {
            for (int y=0; y < blocksize; y++)
            {
                sum += rightblockedsamples[i, y];
            }
            averageBlockRight[i] = (sum/1024);
            sum = 0;
        }
    }
    void EnergyRight()
    {
        float sum = 0;
        for (int i = 0; i < blockcount; i++)
        {
            for (int y=0; y < blocksize; y++)
            {
                sum += Mathf.Abs(rightblockedsamples[i, y]);
            }
            RightaverageBlockEnergy[i] = (sum/1024);
            sum = 0;
        }
    }

    void RMSRight(){
        float sum = 0;
        for (int i = 0; i < blockcount; i++)
        {
            for (int y=0; y < blocksize; y++)
            {
                sum += ((rightblockedsamples[i, y]) * (rightblockedsamples[i, y]));
            }
            RMSright[i] = Mathf.Sqrt((sum/1024));
            sum = 0;
        }
    }

 void RMSSlidingRight()
    {
    float sum = 0;
    int validblock = 0;
    for  (int x=0; x < slide; x++)
        {
            for (int y = 0; y < windowsize; y++)
            {
                int blockidx = x + y;
                if (blockidx >= blockcount) break;
                for (int z = 0; z < blocksize; z++)
                {
                    sum += ((rightblockedsamples[blockidx, z]) * (rightblockedsamples[blockidx, z]));
                }
                validblock++;
            }
            RMSSlidingright[x] = Mathf.Sqrt((sum/(1024*validblock)));
            sum = 0;
            validblock=0;
        }
    }

// Left Process

    void LeftChannel()
    {
        int y=0;
        for (int i = 0; i < allsamplesLength; i = i + 2)
        {
            leftsamples[y] = samples[i];
            y++;
        }
    }

    void LeftChannelBlock(){
        int z = 0;
        for (int i = 0; i < blockcount; i++)
        {
            for (int y=0; y < blocksize; y++)
            {   
                if (z == (singlechannelsamplesLength - 1))
                    break;
                leftblockedsamples[i, y] = leftsamples[z];
                z++;
            }
        }
    }

    void AverageBlockLeft()
    {
        float sum = 0;
        for (int i = 0; i < blockcount; i++)
        {
            for (int y=0; y < blocksize; y++)
            {
                sum += leftblockedsamples[i, y];
            }
            averageBlockLeft[i] = (sum/1024);
            sum = 0;
        }
    }
        void EnergyLeft()
    {
        float sum = 0;
        for (int i = 0; i < blockcount; i++)
        {
            for (int y=0; y < blocksize; y++)
            {
                sum += Mathf.Abs(leftblockedsamples[i, y]);
            }
            LeftaverageBlockEnergy[i] = (sum/1024);
            sum = 0;
        }
    }

    void RMSLeft(){
    float sum = 0;
    for (int i = 0; i < blockcount; i++)
        {
        for (int y=0; y < blocksize; y++)
            {
                sum += ((leftblockedsamples[i, y]) * (leftblockedsamples[i, y]));
            }
            RMSleft[i] = Mathf.Sqrt((sum/1024));
            sum = 0;
        }
    }
    
    void RMSSlidingLeft()
    {
    float sum = 0;
    int validblock = 0;
    for  (int x=0; x < slide; x++)
        {
            for (int y = 0; y < windowsize; y++)
            {
                int blockidx = x + y;
                if (blockidx >= blockcount) break;
                for (int z = 0; z < blocksize; z++)
                {
                    sum += ((leftblockedsamples[blockidx, z]) * (leftblockedsamples[blockidx, z]));
                }
                validblock++;
            }
            RMSSlidingleft[x] = Mathf.Sqrt((sum/(1024*validblock)));
            sum = 0;
            validblock=0;
        }
    }


    //Amplitude
    private float[] TodbFS(float[] samples)
    {
        float[] converted = new float[samples.Length];
        for(int i = 0; i < samples.Length; i++)
        {
            if(samples[i]== 0)
            {
                converted[i] = 20f * Mathf.Log10(Mathf.Abs(1e-6f)); 
                continue;
            }
            converted[i] = 20f * Mathf.Log10(Mathf.Abs(samples[i]));
        }
        return converted;
    } 
   
}