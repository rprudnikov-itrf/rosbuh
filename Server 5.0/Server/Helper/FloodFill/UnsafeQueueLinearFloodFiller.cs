using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using PictureBoxScroll;

namespace FloodFill2
{

    /// <summary>
    /// Implements the QueueLinear flood fill algorithm using pointer-based pixel manipulation.
    /// </summary>
    public class UnsafeQueueLinearFloodFiller
    {
        //public static readonly object lockObject = new Object();

        protected unsafe byte* scan0;
        FloodFillRangeQueue ranges = new FloodFillRangeQueue();

        protected EditableBitmap bitmap;
        protected byte[] tolerance = new byte[] { 25, 25, 25 };
        protected Color fillColor = Color.Magenta;

        //cached bitmap properties
        protected int bitmapWidth = 0;
        protected int bitmapHeight = 0;
        protected int bitmapStride = 0;
        protected int bitmapPixelFormatSize = 0;
        protected byte[] bitmapBits = null;


        //internal, initialized per fill
        protected bool[] pixelsChecked;
        protected byte[] byteFillColor;
        protected byte[] startColor;

        public Color FillColor
        {
            get { return fillColor; }
            set { fillColor = value; }
        }


        public byte[] Tolerance
        {
            get { return tolerance; }
            set { tolerance = value; }
        }

        public EditableBitmap Bitmap
        {
            get { return bitmap; }
            set
            {
                bitmap = value;
            }
        }


        protected void PrepareForFloodFill(Point pt)
        {
            //cache data in member variables to decrease overhead of property calls
            //this is especially important with Width and Height, as they call
            //GdipGetImageWidth() and GdipGetImageHeight() respectively in gdiplus.dll - 
            //which means major overhead.
            byteFillColor = new byte[] { fillColor.B, fillColor.G, fillColor.R, fillColor.A };
            bitmapStride = bitmap.Stride;
            bitmapPixelFormatSize = bitmap.PixelFormatSize;
            bitmapBits = bitmap.Bits;
            bitmapWidth = bitmap.Bitmap.Width;
            bitmapHeight = bitmap.Bitmap.Height;

            pixelsChecked = new bool[bitmapBits.Length / bitmapPixelFormatSize];
        }

        public UnsafeQueueLinearFloodFiller() 
        { 
        }

        public void FloodFill(System.Drawing.Point pt)
        {
            PrepareForFloodFill(pt);
 
            unsafe
            {
                bitmapStride = bitmap.Stride;
                scan0 = (byte*)bitmap.BitPtr;
                int x = pt.X; int y = pt.Y;
                int loc = CoordsToIndex(ref x, ref y);
                byte* colorPtr = ((byte*)(scan0 + loc));
                startColor = new byte[] { colorPtr[0], colorPtr[1], colorPtr[2] };
                LinearFloodFill4(ref x, ref y);

                bool[] pixelsChecked=this.pixelsChecked;

                while (ranges.Count > 0)
                {
                    FloodFillRange range = ranges.Dequeue();

                    //START THE LOOP UPWARDS AND DOWNWARDS
                    int upY = range.Y - 1;//so we can pass the y coord by ref
                    int downY = range.Y + 1;
                    byte* upPtr = (byte*)(scan0 + CoordsToIndex(ref range.StartX, ref upY));
                    byte* downPtr = (byte*)(scan0 + CoordsToIndex(ref range.StartX, ref downY));
                    int downPxIdx = (bitmapWidth * (range.Y + 1)) + range.StartX;//CoordsToPixelIndex(range.StartX,range.Y+1);
                    int upPxIdx = (bitmapWidth * (range.Y - 1)) + range.StartX;//CoordsToPixelIndex(range.StartX, range.Y - 1);
                    for (int i = range.StartX; i <= range.EndX; i++)
                    {
                        //START LOOP UPWARDS
                        //if we're not above the top of the bitmap and the pixel above this one is within the color tolerance
                        if (range.Y > 0 && CheckPixel(ref upPtr) && (!(pixelsChecked[upPxIdx])))
                            LinearFloodFill4(ref i, ref upY);
                        //START LOOP DOWNWARDS
                        if (range.Y < (bitmapHeight - 1) && CheckPixel(ref downPtr) && (!(pixelsChecked[downPxIdx])))
                            LinearFloodFill4(ref i, ref downY);
                        upPtr += bitmapPixelFormatSize;
                        downPtr += bitmapPixelFormatSize;
                        downPxIdx++;
                        upPxIdx++;
                    }
                }
            }
        }

        unsafe void LinearFloodFill4(ref int x, ref int y)
        {

            //offset the pointer to the point passed in
            byte* p = (byte*)(scan0 + (CoordsToIndex(ref x, ref y)));

            //cache some bitmap and fill info in local variables for a little extra speed
            bool[] pixelsChecked=this.pixelsChecked;
            byte[] byteFillColor= this.byteFillColor;
            int bitmapPixelFormatSize=this.bitmapPixelFormatSize;
            int bitmapWidth=this.bitmapWidth;

            //FIND LEFT EDGE OF COLOR AREA
            int lFillLoc = x; //the location to check/fill on the left
            byte* ptr = p; //the pointer to the current location
            int pxIdx = (bitmapWidth * y) + x;
            while (true)
            {
                ptr[0] = byteFillColor[0]; 	 //fill with the color
                ptr[1] = byteFillColor[1];
                ptr[2] = byteFillColor[2];
                if (bitmapPixelFormatSize == 4)
                {
                    ptr[3] = byteFillColor[3];
                }
                pixelsChecked[pxIdx] = true;
                lFillLoc--; 		 	 //de-increment counter
                ptr -= bitmapPixelFormatSize;				 	 //de-increment pointer
                pxIdx--;
                if (lFillLoc <= 0 || !CheckPixel(ref ptr) || (pixelsChecked[pxIdx]))
                    break;			 	 //exit loop if we're at edge of bitmap or color area

            }
            lFillLoc++;

            //FIND RIGHT EDGE OF COLOR AREA
            int rFillLoc = x; //the location to check/fill on the left
            ptr = p;
            pxIdx = (bitmapWidth * y) + x;
            while (true)
            {
                ptr[0] = byteFillColor[0]; 	 //fill with the color
                ptr[1] = byteFillColor[1];
                ptr[2] = byteFillColor[2];
                if (bitmapPixelFormatSize == 4)
                {
                    ptr[3] = byteFillColor[3];
                }

                pixelsChecked[pxIdx] = true;
                rFillLoc++; 		 //increment counter
                ptr += bitmapPixelFormatSize;				 //increment pointer
                pxIdx++;
                if (rFillLoc >= bitmapWidth || !CheckPixel(ref ptr) || (pixelsChecked[pxIdx]))
                    break;			 //exit loop if we're at edge of bitmap or color area

            }
            rFillLoc--;

            FloodFillRange r = new FloodFillRange(lFillLoc, rFillLoc, y);
            ranges.Enqueue(ref r);

        }

        private unsafe bool CheckPixel(ref byte* px)
        {
            return
                px[0] >= (startColor[0] - tolerance[0]) && px[0] <= (startColor[0] + tolerance[0]) &&
                px[1] >= (startColor[1] - tolerance[1]) && px[1] <= (startColor[1] + tolerance[1]) &&
                px[2] >= (startColor[2] - tolerance[2]) && px[2] <= (startColor[2] + tolerance[2]);
        }

        private int CoordsToIndex(ref int x, ref int y)
        {
            return (bitmapStride * y) + (x * bitmapPixelFormatSize);
        }

    }
}
