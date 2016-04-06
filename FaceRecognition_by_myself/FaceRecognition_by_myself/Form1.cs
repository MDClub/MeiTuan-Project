using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace  FaceRecognition_by_myself
{
    public partial class Form1 : Form
    {
        #region variables
        // webcam
        Capture webcam;
        Capture cptvideo;
        int VideoFps;//视频帧率
        bool cameraInUse = false;
        bool captureInProgress = false;
        OpenFileDialog openFileDialog = new OpenFileDialog();
        [DllImport("FDDll.dll", EntryPoint = "Mytest", CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern int* Mytest(int whidth, int height, IntPtr pImg);

        MCvFont font = new MCvFont(
            Emgu.CV.CvEnum.FONT.CV_FONT_HERSHEY_TRIPLEX,
            1.2,
            1.2
            );

        Image<Bgr, byte> current;
        Image<Gray, byte> gray = null;
        Image<Bgr, byte> imgshow = null;


        List<Image<Gray, byte>> learnedFaces = new List<Image<Gray, byte>>();
        List<string> learnedPeople = new List<string>();

        unsafe int* pf = null;
        #endregion

        #region initiates
        public Form1()
        {
            InitializeComponent();
        }

        //private void initHaar(ref HaarCascade h, string path)
        //{
        //    h = new HaarCascade(path);
        //}

        // try load early trained faces
        private void loadBackupImages(List<Image<Gray, byte>> list)
        {
          
        }
        #endregion

        #region private helpers
        // initiate webcam
        private bool initiateWebCam(int index)
        {
            if (webcam != null)
                return false;

            try
            {
                webcam = new Capture(index);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("WEBCAM:" + ex.Message);
                return false;
            }
        }

        // process camera
        private void processCamera(object sender, EventArgs e)
        {
            current = webcam.QueryFrame();
            // flip because my webcam is MIRRORED
            current = current.Flip(Emgu.CV.CvEnum.FLIP.HORIZONTAL);
            imageBox.Image = current.Bitmap;
        }

        #endregion

        private void  ProcessFrame(object sender, EventArgs e)
        {
            current =  cptvideo.QueryFrame();           
            //为使播放顺畅，添加以下延时
            System.Threading.Thread.Sleep((int)(1000.0 / VideoFps) - 20);
            if (current != null)
            {
                imageBox.Image = current.Bitmap;
            }
                       
        }

        #region analyze
       

        private void markFaces(Image<Bgr, byte> img)
        {
            if (img == null)
                return;
            Bitmap image = img.ToBitmap();
            IntPtr pImg = IntPtr.Zero;
            //System.Runtime.InteropServices.Marshal.StructureToPtr(image, pImg, false);



            //锁定Bitmap数据
            BitmapData bmpData = image.LockBits(
            new Rectangle(0, 0, image.Width, image.Height),
            System.Drawing.Imaging.ImageLockMode.ReadWrite, image.PixelFormat);

            IntPtr ptrImg = bmpData.Scan0;
            unsafe
            {
                //int* pf = null;
                DateTime beforDT = System.DateTime.Now;
                pf = Mytest(image.Width, image.Height, ptrImg);
                DateTime afterDT = System.DateTime.Now;
                TimeSpan ts = afterDT.Subtract(beforDT);
                Console.WriteLine("DateTime总共花费{0}ms.", ts.TotalMilliseconds);

                foundPeople.Clear();
                if (pf == null||*pf == 0)
                    return;


                if (checkBox1.Checked)
                    // IF FOUND, AUTO STOP
                    if (*pf > 0)
                        startButton_Click(null, null);

                for (int i = 0; i < *pf; i++)
                {
                    //MCvAvgComp face;
                    //face.rect.X
                    Rectangle facerect = new Rectangle(*(pf + 4 * i + 1), *(pf + 4 * i + 2), *(pf + 4 * i + 3), *(pf + 4 * i + 4));

                    current.Draw(facerect, new Bgr(Color.Lime), 2);

                    //MCvAvgComp face = faces[i];
                    Image<Gray, byte> result = current
                        .Copy(facerect)
                        .Convert<Gray, byte>()
                        .Resize(64, 64, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

                    //current.Draw(face.rect, new Bgr(Color.Lime), 2);

                    // RECOGNIZE!
                    if (learnedFaces.Count > 0)
                    {
                        //TermCriteria 
                        //for face recognition
                        MCvTermCriteria tc = new MCvTermCriteria(learnedFaces.Count, 0.001);

                        //Eigen face recognizer
                        EigenObjectRecognizer recognizer = new EigenObjectRecognizer(
                           learnedFaces.ToArray(),
                           learnedPeople.ToArray(),
                           3000,
                           ref tc);
                        string name = "";
                        if (recognizer.Recognize(result) != null)
                            name = recognizer.Recognize(result).Label;//改过
                        foundPeople[name] = facerect;
                        //current.Draw(name,ref font,new Point(face.rect.X - 2, face.rect.Y - 2),new Bgr(Color.Blue));
                    }

                }
                imageBox.Image = current.Bitmap;
            }
        }

        Dictionary<string, Rectangle> foundPeople = new Dictionary<string, Rectangle>();

        float xfactor;
        float yfactor;

        private void imageBox_Paint(object sender, PaintEventArgs e)
        {
            if (foundPeople.Count > 0&&current!=null)
            {
                // 缩放
                xfactor = (float)imageBox.Width / (float)current.Bitmap.Width;
                yfactor = (float)imageBox.Height / (float)current.Bitmap.Height;

                foreach (string name in foundPeople.Keys)
                {
                    e.Graphics.DrawString(
                        name,
                        new Font("verdana", 12),//this.Font,
                        Brushes.Red,
                        foundPeople[name].X * xfactor,
                        foundPeople[name].Y * yfactor);
                    e.Graphics.DrawRectangle(new Pen(Color.Red), 
                        foundPeople[name].X * xfactor,
                        foundPeople[name].Y * yfactor,
                        foundPeople[name].Width * xfactor,
                        foundPeople[name].Height * yfactor);
                }
            }
        }

        #endregion

        #region action
        // process faces (do this while idle, that is every moment)
        private void processFaces(object sender, EventArgs arg)
        {
            //foundFaces = getFaces(current, faceHaar);
            //markFaces(foundFaces);
            markFaces(current);
        }

        //start/stop
        private void startButton_Click(object sender, EventArgs e)
        {
            this.timer1.Interval  = Convert.ToInt16(Timer_textBox.Text);
            if (radioButton1.Checked)
            {
                //pic mode

                openFileDialog.Filter = "所有文件|*.*|JPG文件|*.jpg|PNG文件|*.png|BMP文件|*.bmp|JPEG文件|*.jpeg";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    //current = new Image<Bgr, byte>("1.jpg");
                    current = new Image<Bgr, byte>(openFileDialog.FileName);
                    current = current.Flip(Emgu.CV.CvEnum.FLIP.HORIZONTAL);
                    imageBox.Image = current.Bitmap;
                    checkBox1.Checked = false;
                    markFaces(current);
                }
                //cptvideo = null;
            }
            else if (radioButton2.Checked)
            {


                // webcam ok
                if (cameraInUse)
                {
                    Application.Idle -= new EventHandler(processCamera);
                    timer1.Tick -= new EventHandler(processFaces);
                }
                else
                {
                    if (webcam == null)
                        return;
                    Application.Idle += new EventHandler(processCamera);
                    timer1.Tick += new EventHandler(processFaces);
                }
                cameraInUse = !cameraInUse;

                radioButton1.Enabled = !cameraInUse;
                radioButton3.Enabled = !cameraInUse;
                //cptvideo = null;
            }
            else
            {
                if (cptvideo == null)
                {
                    try
                    {
                        openFileDialog.Filter = "所有文件|*.*|AVI文件|*.avi|RMVB文件|*.rmvb|WMV文件|*.wmv|MKV文件|*.mkv|MP4文件|*.mp4";
                        if (openFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            cptvideo = new Capture(openFileDialog.FileName);
                            VideoFps = (int)CvInvoke.cvGetCaptureProperty(cptvideo, Emgu.CV.CvEnum.CAP_PROP.CV_CAP_PROP_FPS);
                        }
                        
                    }
                    catch (NullReferenceException excpt)
                    {
                        MessageBox.Show(excpt.Message);
                    }
                }
                if (cptvideo != null)
                {
                    if (captureInProgress)
                    {  //stop the capture
                        Application.Idle -= new EventHandler(ProcessFrame);
                        timer1.Tick -= new EventHandler(processFaces);
                        startButton.Text = "Play";
                    }
                    else
                    {
                        //start the capture
                        Application.Idle += new EventHandler(ProcessFrame);
                        timer1.Tick += new EventHandler(processFaces);
                        startButton.Text = "Pause";
                    }

                    captureInProgress = !captureInProgress;
                }
                radioButton1.Enabled = !captureInProgress;
                radioButton2.Enabled = !captureInProgress;
            }
        }

        // first load & run

        private void Form1_Load(object sender, EventArgs e)
        {

            // initiates with old backup files
            loadBackupImages(learnedFaces);
            //webcam mode
            if (!initiateWebCam(0))
            {
                MessageBox.Show("Failed to initiate webcam\r\nAre you sure you have one?");
                return;
            }            
            
        }

        // webcam mode
        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked)
            {
                startButton.Text = "Strt/Stp";
                toolTip1.SetToolTip(startButton, "启动/停止摄像头");
                current = null;
                imageBox.Image = null;
                cptvideo = null;
                foundPeople.Clear();
            }
        }

        //pic mode
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                startButton.Text = "Open";
                toolTip1.SetToolTip(startButton, "开始识别脑壳");
                current = null;
                imageBox.Image = null;
                cptvideo = null;
                foundPeople.Clear();
            }
        }
        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton3.Checked)
            {
                startButton.Text = "Open";
                toolTip1.SetToolTip(startButton, "开始识别人脸");
                current = null;
                imageBox.Image = null;
                foundPeople.Clear();
            }
        }

        // learn people
        private void button2_Click(object sender, EventArgs e)
        {
            unsafe
            {

                if (pf == null || *pf == 0)
                    return;

                string name = "";

                for (int i = 0; i < *pf; i++)
                {
                    Rectangle facerect = new Rectangle(*(pf + 4 * i + 1), *(pf + 4 * i + 2), *(pf + 4 * i + 3), *(pf + 4 * i + 4));
                    name = "Unknown" + learnedFaces.Count;
                    imgshow = current
                        .Copy(facerect)
                        .Resize(64, 64, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                    gray = current
                        .Copy(facerect)
                        .Convert<Gray, byte>()
                        .Resize(64, 64, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

                    learnedFaces.Add(gray);
                    learnedPeople.Add(name);

                    imageList1.Images.Add(imgshow.Bitmap);
                    listView1.Items.Add(name);
                    listView1.Items[listView1.Items.Count - 1].ImageIndex =
                        imageList1.Images.Count - 1;
                }
            }
        }

        // udpate count of learned
        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            label2.Text = string.Format("Learned faces:{0}", listView1.Items.Count);
        }

        // ...
        private void listView1_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Label))
                e.CancelEdit = true;
            else
                // Check to ensure no duplicated names
                if (listView1.Items.ContainsKey(e.Label))
                    e.CancelEdit = true;
                else
                    learnedPeople[e.Item] = e.Label;
        }
        #endregion

        private void Timer_KeyPress(object sender, KeyPressEventArgs e)
        {
            char result = e.KeyChar;
            if (char.IsDigit(result) || result == 8)
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }



    }
}