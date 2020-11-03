using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace PerlinStandalone
{
    class Program
    {
        static int ImgRes = 512;
        static byte[] _imageBuffer = new byte[ImgRes * ImgRes * 4];

        static void PlotPixel(int x, int y, byte r, byte g, byte b)
        {
            int Offset = ((ImgRes * 4) * y) + (x * 4);

            _imageBuffer[Offset] = b;
            _imageBuffer[Offset + 1] = g;
            _imageBuffer[Offset + 2] = r;
            _imageBuffer[Offset + 3] = 255;
        }

        static void SaveNormalMapAsImage(float[,] Map, int ImgRes , string Name)
        {
            for (int x = 0; x < ImgRes; x++)
            {
                for (int y = 0; y < ImgRes; y++)
                {
                    int PVal = (int)(Map[x, y] * 255);
                    PlotPixel(x, y, (byte)PVal, (byte)PVal, (byte)PVal);
                }
            }

            unsafe
            {
                fixed (byte* ptr = _imageBuffer)
                {
                    using (Bitmap image = new Bitmap(ImgRes, ImgRes, ImgRes * 4,
                        System.Drawing.Imaging.PixelFormat.Format32bppRgb, new IntPtr(ptr)))
                    {
                        image.Save(@Name+".jpg");
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            Perlin2D perlinEng = new Perlin2D();
            float[,] PMap = perlinEng.GetNormalMap(ImgRes);

            OctavePerlin octPerlin = new OctavePerlin(0.5f, 2f);
            float[,] OMap = octPerlin.GetNormalOctavePerlin(6, ImgRes, 3);

            SaveNormalMapAsImage(PMap, ImgRes, "PerlinMap");
            SaveNormalMapAsImage(OMap, ImgRes, "OctavePerlinMap");        
        }
    }

    class Vector2 { public float x; public float y; public Vector2(float X = 0f, float Y = 0f) { x = X; y = Y; } }

    class OctavePerlin
    {
        float Persistence;
        float Lacunarity;
        int Seed;

        public OctavePerlin(float Per = 0.6f, float Lac = 2, int seed = 1)
        {
            Persistence = Per;
            Lacunarity = Lac;
            Seed = seed;
        }

        public float[,] GetNormalOctavePerlin(int Octaves, int Res, int StartingPerlinRes)
        {
            float[,] Output = new float[Res, Res];

            float OctaveRes = StartingPerlinRes;
            float Intensity = 1;

            float min = float.MaxValue;
            float max = float.MinValue;

            for(int Oc = 0; Oc < Octaves; Oc++)
            {
                Perlin2D p = new Perlin2D((int)OctaveRes, Seed);

                float[,] OctaveMap = p.GetNormalMap(Res);

                for(int i = 0; i < Res; i++)
                {
                    for(int j = 0; j < Res; j++)
                    {
                        Output[i, j] += ((OctaveMap[i, j] * 2) - 1) * Intensity;

                        min = min < Output[i, j] ? min : Output[i, j];
                        max = max > Output[i, j] ? max : Output[i, j];
                    }
                }

                OctaveRes *= Lacunarity;
                Intensity *= Persistence;
            }


            //Normalise
            float Difference = max - min;
            float Ratio = 1 / Difference;

            for (int x = 0; x < Res; x++)
            {
                for (int y = 0; y < Res; y++)
                {
                    Output[x, y] -= min;
                    Output[x, y] *= Ratio;
                }
            }

            return Output;
        }
    }

    class Perlin2D
    {
        int Seed;
        int Resolution;

        System.Random rand;
        Vector2[,] Gradients;

        float max;
        float min;

        float GetMax() { return max; }
        float GetMin() { return min; }

        public Perlin2D(int resolution = 3, int seed = 1)
        {
            Seed = seed;
            rand = new System.Random(Seed);
            Resolution = resolution;

            //Initialise gradients with random vectors
            Gradients = new Vector2[Resolution, Resolution];
            RandomiseGradients();
            //SetSpecificGradients();
        }

        //Recieves a coordinate between 0 and resolution - 1, and returns its perlin value in the grid.
        public float GetPerlinValue(float x, float y)
        {
            if (x < 0 || x >= Resolution - 1 || y < 0 || y >= Resolution - 1)
                return 0.5f;

            //Console.Write("Sampling coord: " + x + ", " + y + "\n");

            //Bottom-left coordinate of unit square our point is in
            int FloorX = (int)x;
            int FloorY = (int)y;

            //Console.Write("Testing unit square: " + FloorX + ", " + FloorY + "\n");

            float U = x - FloorX;
            float V = y - FloorY;

            Vector2 tl = new Vector2(U, V);
            Vector2 tr = new Vector2(U - 1, V);
            Vector2 bl = new Vector2(U, V - 1);
            Vector2 br = new Vector2(U - 1, V - 1);

            float TLdot = DotProduct(tl, Gradients[FloorX, FloorY]);
            float TRdot = DotProduct(tr, Gradients[FloorX + 1, FloorY]);
            float BLdot = DotProduct(bl, Gradients[FloorX, FloorY + 1]);
            float BRdot = DotProduct(br, Gradients[FloorX + 1, FloorY + 1]);


            float softU = Ease(U);
            float softV = Ease(V);

            float TopLerp = Lerp(TLdot, TRdot, softU);
            float BottomLerp = Lerp(BLdot, BRdot, softU);

            float Average = Lerp(TopLerp, BottomLerp, softV);

            //Console.Write("Sampling value: " + Average + "\n\n");

            return Average;
        }

        public float[,] GetNormalMap(int res)
        {
            float[,] NormalPerlinMap = new float[res, res];

            //Track min and max values so we can normalise
            min = max = GetPerlinValue(0, 0);

            //Work out what increment we need to step by to sample evenly across the whole perlin space
            float Increment = (float)(((float)Resolution - 1f) / (float)res);

            float xCoord = 0f;
            float yCoord = 0f;

            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    NormalPerlinMap[x, y] = GetPerlinValue(xCoord, yCoord);

                    min = min < NormalPerlinMap[x, y] ? min : NormalPerlinMap[x, y];
                    max = max > NormalPerlinMap[x, y] ? max : NormalPerlinMap[x, y];

                    yCoord += Increment;

                    //Console.Write("PRE normal value " + x + y + ":" + NormalPerlinMap[x, y] + "\n");
                }
                yCoord = 0f;
                xCoord += Increment;
            }

            //Normalise
            float Difference = max - min;
            float Ratio = 1 / Difference;

            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    NormalPerlinMap[x, y] -= min;
                    NormalPerlinMap[x, y] *= Ratio;

                    //Console.Write("normal value " + x + y + ":" + NormalPerlinMap[x, y] + "\n");
                }
            }

            return NormalPerlinMap;
        }

        float DotProduct(Vector2 A, Vector2 B)
        {
            return (A.x * B.x) + (A.y * B.y);
        }

        void RandomiseGradients()
        {
            for (int x = 0; x < Resolution; x++)
                for (int y = 0; y < Resolution; y++)
                    Gradients[x, y] = RandomNormalVector();
        }

        //ONLY USE for 3x3
        void SetSpecificGradients()
        {
            for (int x = 0; x < Resolution; x++)
                for (int y = 0; y < Resolution; y++)
                    Gradients[x, y] = NormaliseVector(new Vector2(1, 1));
        }

        Vector2 RandomNormalVector()
        {
            Vector2 v = new Vector2();

            v.x = RandFloat();
            v.y = RandFloat();

            v = NormaliseVector(v);

            return v;
        }

        Vector2 NormaliseVector(Vector2 v)
        {
            float magnitude = (float)Math.Sqrt((v.x * v.x) + (v.y * v.y));

            v.x /= magnitude;
            v.y /= magnitude;

            return v;
        }

        float RandFloat()
        {
            double u1 = 1.0 - rand.NextDouble();
            double u2 = 1.0 - rand.NextDouble();

            double randStdNormal = Math.Sqrt((float)-2.0 * Math.Log((float)u1));
            randStdNormal *= Math.Sin((float)(2.0 * Math.PI * u2));

            return (float)randStdNormal;
        }

        float Ease(float val)
        { return val * val * val * (val * (val * 6 - 15) + 10); }

        float Lerp(float a, float b, float x)
        { return a + x * (b - a); }
    }

}

