using Safari;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace Safari
{
    public partial class MainForm : Form
    {
        // List of all lakes in the simulation
        private List<Lake> lakes;
        // Mapping of each lake to its position on the screen
        private Dictionary<Lake, Point> lakePositions;
        // Random generator to assign animals to random lakes
        private Random rand = new Random();
        // List of all animals spawned during the simulation
        private List<Animal> animals = new List<Animal>();
        // Timers for spawning animals periodically
        private System.Windows.Forms.Timer flamingoTimer;
        private System.Windows.Forms.Timer zebraTimer;
        private System.Windows.Forms.Timer hippoTimer;

        public MainForm()
        {
            // Optimize rendering performance
            this.DoubleBuffered = true;
            // Fullscreen window with background image
            this.WindowState = FormWindowState.Maximized;
            this.BackgroundImage = Image.FromFile("image/safari.jpg");
            this.BackgroundImageLayout = ImageLayout.Stretch;

            InitializeLakes();
            HookLakeEvents();

            // "Start" button
            Button startButton = new Button
            {
                Text = "Start",
                Location = new Point(20, 20),
                Size = new Size(80, 30)
            };
            startButton.Click += StartButton_Click;
            Controls.Add(startButton);

            // "Stop" button
            Button stopButton = new Button
            {
                Text = "Stop",
                Location = new Point(120, 20),
                Size = new Size(80, 30)
            };
            stopButton.Click += StopButton_Click;
            Controls.Add(stopButton);

            // Timers for animal creation at intervals
            flamingoTimer = new System.Windows.Forms.Timer();
            flamingoTimer.Interval = 2000;
            flamingoTimer.Tick += (s, e) => SpawnAnimal(AnimalType.Flamingo, 3500);

            zebraTimer = new System.Windows.Forms.Timer();
            zebraTimer.Interval = 3000;
            zebraTimer.Tick += (s, e) => SpawnAnimal(AnimalType.Zebra, 5000);

            hippoTimer = new System.Windows.Forms.Timer();
            hippoTimer.Interval = 10000;
            hippoTimer.Tick += (s, e) => SpawnAnimal(AnimalType.Hippopotamus, 5000);
        }

        // Initialize lakes and their screen positions
        private void InitializeLakes()
        {
            lakes = new List<Lake>
            {
                new Lake("A", 5),
                new Lake("B", 7),
                new Lake("C", 10)
            };

            lakePositions = new Dictionary<Lake, Point>();
            int width = Screen.PrimaryScreen.Bounds.Width;
            int height = Screen.PrimaryScreen.Bounds.Height;

            lakePositions[lakes[0]] = new Point(width / 4, height / 3);
            lakePositions[lakes[1]] = new Point(width / 2, height / 2);
            lakePositions[lakes[2]] = new Point(3 * width / 4, 2 * height / 3);
        }

        // Subscribes to each lake's status change event to trigger UI refresh
        private void HookLakeEvents()
        {
            foreach (var lake in lakes)
            {
                lake.OnStatusChanged = () =>
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(Invalidate));
                    }
                };
            }
        }

        // Draw lakes and their occupancy when the form repaints
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            foreach (var lake in lakes)
            {
                DrawLake(g, lake, lakePositions[lake]);
                DrawLakeOccupancy(g, lake, lakePositions[lake]);
            }
        }

        // Draws a single lake image
        private void DrawLake(Graphics g, Lake lake, Point position)
        {
            Image lakeImage = Image.FromFile("image/L.png");
            int size = 140 + lake.Capacity * 5;
            int half = size / 2;
            g.DrawImage(lakeImage, position.X - half, position.Y - half, size, size);
        }

        // Draws animals around a lake based on their assigned slots
        private void DrawLakeOccupancy(Graphics g, Lake lake, Point lakeCenter)
        {
            var occupancy = lake.GetCurrentAnimals();
            int radius = 80 + lake.Capacity * 6;
            double angleStep = 2 * Math.PI / lake.Capacity;
            HashSet<Animal> drawnAnimals = new HashSet<Animal>();

            for (int i = 0; i < lake.Capacity; i++)
            {
                double angle = i * angleStep;
                int x = lakeCenter.X + (int)(radius * Math.Cos(angle)) - 30;
                int y = lakeCenter.Y + (int)(radius * Math.Sin(angle)) - 30;

                Rectangle slot = new Rectangle(x, y, 60, 60);
                Animal current = occupancy[i];

                if (current != null && !drawnAnimals.Contains(current))
                {
                    string imagePath = current.Type switch
                    {
                        AnimalType.Flamingo => "image/F.png",
                        AnimalType.Zebra => "image/Z.png",
                        AnimalType.Hippopotamus => "image/H.png",
                        _ => "image/empty.png"
                    };

                    Image img = Image.FromFile(imagePath);
                    g.DrawImage(img, slot);
                    drawnAnimals.Add(current);
                }
                else if (current == null)
                {
                    g.DrawRectangle(Pens.Gray, slot);
                }
            }
        }

        // Start simulation
        private void StartButton_Click(object sender, EventArgs e)
        {
            flamingoTimer.Start();
            zebraTimer.Start();
            hippoTimer.Start();
        }

        // Stop simulation
        private void StopButton_Click(object sender, EventArgs e)
        {
            flamingoTimer.Stop();
            zebraTimer.Stop();
            hippoTimer.Stop();
        }

        // Spawn a new animal of given type and duration in random lake
        private void SpawnAnimal(AnimalType type, int durationMs)
        {
            Lake lake = lakes[rand.Next(lakes.Count)];
            Animal animal = new Animal(type, lake);
            animals.Add(animal);

            new Thread(() =>
            {
                animal.Live(durationMs);
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(Invalidate));
                }
            }).Start();
        }
    }
}