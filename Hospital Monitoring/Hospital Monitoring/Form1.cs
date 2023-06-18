using ScottPlot.Plottable;
using System.IO.Ports;
using System.Media;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace Hospital_Monitoring
{
    public partial class Form1 : Form
    {
        SerialPort port;

        Dictionary<DateTime, double> tempValues = new Dictionary<DateTime, double>();
        Dictionary<DateTime, double> humValues = new Dictionary<DateTime, double>();
        ScatterPlot scatterplot = null;

        SoundPlayer Alert = new System.Media.SoundPlayer(@"C:\Users\Kirollos\source\repos\Hospital Monitoring\Hospital Monitoring\NewMessage.wav");

        Queue<string> TXFrameBuffer = new Queue<string>();
        //Queue<string> RXFrameBuffer = new Queue<string>();


        int currentTemp, currentHum, currentSmoke;

        int tempThreshold;
        int smokeThreshold;

        bool ACoverride = false, Buzzeroverride = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //port = new SerialPort("COM10", 9600);
            //port.Open();
            comboBox1.DataSource = SerialPort.GetPortNames();
            label2.Text = "Disconnected";
            label2.ForeColor = Color.Red;
            tempThreshold = int.Parse(textBox4.Text);
            smokeThreshold = int.Parse(textBox5.Text);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (port != null && port.IsOpen)
            {
                port.DataReceived -= Port_DataReceived;
                port.Close();
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            comboBox1.DataSource = SerialPort.GetPortNames();
        }

        private void RenderTempGraph()
        {
            formsPlot.Plot.Clear();
            // generate sample data (arrays of individual DateTimes and values)
            int pointCount = 600;
            Random rand = new Random(0);
            //double[] values = ScottPlot.DataGen.RandomWalk(rand, pointCount);
            DateTime[] dates = Enumerable.Range(0, pointCount)
                                          .Select(x => DateTime.Now.AddMinutes(x))
                                          .ToArray();

            // use LINQ and DateTime.ToOADate() to convert DateTime[] to double[]
            double[] xs = /*dates*/tempValues.Keys.Select(x => x.ToOADate()).ToArray();

            var plt = formsPlot.Plot;
            // plot the double arrays using a traditional scatter plot
            scatterplot = plt.AddScatter(xs, tempValues.Values.ToArray());

            // indicate the horizontal axis tick labels should display DateTime units
            plt.XAxis.DateTimeFormat(true);

            // add padding to the right to prevent long dates from flowing off the figure
            plt.YAxis2.SetSizeLimit(min: 40);

            // save the output
            plt.Title("Temperature Measurement");
            try { formsPlot.Refresh(); }
            catch
            {

            }
        }

        private void AddTempData(double temp)
        {
            tempValues.Add(DateTime.Now, /*double.Parse(textBox1.Text)*/temp);
            double[] xs = /*dates*/tempValues.Keys.Select(x => x.ToOADate()).ToArray();
        }

        private void RenderHumGraph()
        {
            formsPlot1.Plot.Clear();
            // generate sample data (arrays of individual DateTimes and values)
            int pointCount = 600;
            Random rand = new Random(0);
            //double[] values = ScottPlot.DataGen.RandomWalk(rand, pointCount);
            DateTime[] dates = Enumerable.Range(0, pointCount)
                                          .Select(x => DateTime.Now.AddMinutes(x))
                                          .ToArray();

            // use LINQ and DateTime.ToOADate() to convert DateTime[] to double[]
            double[] xs = /*dates*/humValues.Keys.Select(x => x.ToOADate()).ToArray();

            var plt = formsPlot1.Plot;
            // plot the double arrays using a traditional scatter plot
            scatterplot = plt.AddScatter(xs, humValues.Values.ToArray());

            // indicate the horizontal axis tick labels should display DateTime units
            plt.XAxis.DateTimeFormat(true);

            // add padding to the right to prevent long dates from flowing off the figure
            plt.YAxis2.SetSizeLimit(min: 40);

            // save the output
            plt.Title("Humidity Measurement");
            try { formsPlot1.Refresh(); }
            catch
            {

            }
        }

        private void AddHumData(double hum)
        {
            humValues.Add(DateTime.Now, /*double.Parse(textBox1.Text)*/hum);
            double[] xs = /*dates*/humValues.Keys.Select(x => x.ToOADate()).ToArray();
        }

        private void button10_Click(object sender, EventArgs e)
        {
            if (port != null && port.IsOpen)
            {
                port.DataReceived -= Port_DataReceived;
                port.Close();
                label2.Text = "Disconnected";
                label2.ForeColor = Color.Red;
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            if (port != null)
                if (port.IsOpen)
                    port.Close();
            port = new SerialPort(comboBox1.Text, 9600);
            port.Open();
            if (port.IsOpen)
            {
                label2.Text = "Connected";
                label2.ForeColor = Color.Green;
            }
            else
            {
                label2.Text = "Disconnected";
                label2.ForeColor = Color.Red;
            }
            port.DataReceived += Port_DataReceived;
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            char[] buffer = new char[10];
            //int len = port.Read(buffer, 0, 8);
            int len = 0;
            string buff = "";
            bool start = false;
            bool end = false;
            while (true)
            {
                try
                {
                    len = port.Read(buffer, 0, 1);
                }
                catch
                {
                    start = end = false;
                    break;
                }
                for (int i = 0; i < len; i++)
                {
                    if (buffer[i] == '@')
                        start = true;
                    if (start)
                        buff += buffer[i];
                    if (start && buffer[i] == ';') //end
                    {
                        end = true;
                        break;
                    }
                }
                if (end) break;
            }
            //RXFrameBuffer.Enqueue(new string(buff));
            this.Invoke(() => handleRX(new string(buff)));
        }

        void port_write(string frame)
        {
            //if (port != null && port.IsOpen)
            //port.Write(frame);
            TXFrameBuffer.Enqueue(frame);
        }

        private void handleRX(string frame)
        {
            if (frame.StartsWith("@STS"))
            {
                if (frame.StartsWith("@STS:AC"))
                {
                    int idx = frame.IndexOf("AC") + 2;
                    int status = int.Parse(frame.Substring(idx, frame.IndexOf(';') - idx));
                    label8.Text = status > 0 ? "ON" : "OFF";
                    label8.ForeColor = status > 0 ? Color.Green : Color.Red;
                }
                if (frame.StartsWith("@STS:BZ"))
                {
                    int idx = frame.IndexOf("BZ") + 2;
                    int status = int.Parse(frame.Substring(idx, frame.IndexOf(';') - idx));
                    label9.Text = status > 0 ? "ON" : "OFF";
                    label9.ForeColor = status > 0 ? Color.Green : Color.Red;
                }
                return;
            }
            int temp = int.Parse(frame.Substring(1, 2));
            int humidity = int.Parse(frame.Substring(3, 3));
            int smoke = int.Parse(frame.Substring(6, 3));

            handleRX(temp, humidity, smoke);
        }

        private void handleRX(int temp, int humidity, int smoke)
        {
            AddTempData(temp);
            RenderTempGraph();
            AddHumData(humidity);
            RenderHumGraph();
            //double smokevolt = (smoke * 5.0) / 1024;
            textBox1.Text = temp.ToString();
            textBox2.Text = humidity.ToString();
            textBox3.Text = smoke.ToString(); //+ " | " + smokevolt.ToString("0.00") + "V";
            progressBar3.Value = temp;
            progressBar1.Value = humidity;
            progressBar2.Value = smoke * 100 / 900;
            //Console.Beep(500, 1000);
            currentTemp = temp;
            currentHum = humidity;
            currentSmoke = smoke;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            port_write("@A1000000;");
            ACoverride = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            port_write("@A0000000;");
            ACoverride = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            port_write("@B0000000;");
            Buzzeroverride = true;
            alarmcancel = true;
            label14.Text = "Smoke detected!";
            label14.ForeColor = Color.Red;
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            tempThreshold = trackBar1.Value;
            textBox4.Text = trackBar1.Value.ToString();
        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {
            if (textBox4.Text.Length == 0 || int.Parse(textBox4.Text) < 20) return;
            if (int.Parse(textBox4.Text) >= 60)
                textBox4.Text = "60";
            tempThreshold = int.Parse(textBox4.Text);
            trackBar1.Value = int.Parse(textBox4.Text);
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            smokeThreshold = trackBar2.Value;
            textBox5.Text = trackBar2.Value.ToString();
        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {
            if (textBox4.Text.Length == 0 || int.Parse(textBox5.Text) < 50) return;
            if (int.Parse(textBox5.Text) >= 900)
                textBox5.Text = "900";
            smokeThreshold = int.Parse(textBox5.Text);
            trackBar2.Value = int.Parse(textBox5.Text);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (currentTemp > tempThreshold)
            {
                label12.Text = "High temp!";
                label12.ForeColor = Color.Red;
                if (!ACoverride)
                    port_write("@A1000000;");
            }
            else
            {
                label12.Text = "OK";
                label12.ForeColor = Color.Green;
                if (!ACoverride)
                    port_write("@A0000000;");
            }

            if (currentSmoke > smokeThreshold)
            {
                label14.Text = "Smoke detected!";
                label14.ForeColor = Color.Red;
                if (!Buzzeroverride)
                    port_write("@B1000000;");

                timer2.Enabled = true;
            }
            else
            {
                label14.Text = "OK";
                label14.ForeColor = Color.Green;
                if (!Buzzeroverride)
                    port_write("@B0000000;");

                timer2.Enabled = false;
                alarmcancel = false;
            }
        }
        private bool alarmcancel = false;
        private void timer2_Tick(object sender, EventArgs e)
        {
            if (alarmcancel) return;
            Alert.Play();
            label14.Text = "Smoke detected!";
            label14.ForeColor = Color.Red;
            label14.Visible = !label14.Visible;

            if (currentSmoke < smokeThreshold)
            {
                label14.Text = "OK";
                label14.ForeColor = Color.Green;
                if (port.IsOpen)
                    port_write("@B0000000;");
                timer2.Enabled = false;
                Buzzeroverride = false;
            }
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            //if (port != null && port.IsOpen && RXFrameBuffer.Count > 0)
            //{
            //    handleRX(RXFrameBuffer.Dequeue());
            //}
            if (port != null && port.IsOpen && TXFrameBuffer.Count > 0)
            {
                port.Write(TXFrameBuffer.Dequeue());
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            ACoverride = false;
            Buzzeroverride = false;
        }
    }
}