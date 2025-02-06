using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Management;
using LibreHardwareMonitor.Hardware;

//CPU使用率温度
//物理メモリ使用量率
//GPUメモリ使用量率

namespace Test2
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ComputerStatus status = new ComputerStatus();
            Console.CursorVisible = false;

            while (true)
            {
                status.Update();

                Console.WriteLine(status.CpuName);
                Console.WriteLine("物理コア : " + status.CoreCount.ToString() + "  スレッド : " + status.HtCoreCount.ToString());
                for (int i = 0; i < status.CoreTemps.Length; i++)
                {
                    Console.WriteLine("コア{0} : {1:f}℃", i, status.CoreTemps[i]);
                }
                Console.WriteLine("パッケージ温度 : {0:f}℃", status.PackageTemp);
                Console.WriteLine("平均温度 : {0:f}℃", status.CpuTemp);
                for (int j = 0; j < status.HtCoreUsings.Length; j++)
                {
                    Console.WriteLine("コア{0} スレッド{1} : {2:#,#00.00}%", j / 2, j % 2, status.HtCoreUsings[j]);
                }
                Console.WriteLine("平均使用率 : {0:#,#00.00}%", status.CpuUsing);
                Console.WriteLine();
                Console.WriteLine("総メモリ : {0:#,###.00}GB", status.TotalMemory);
                Console.WriteLine("使用メモリ : {0:f}GB", status.UsedMemory);
                Console.WriteLine("空きメモリ : {0:f}GB", status.FreeMemory);
                Console.WriteLine("メモリ使用率 : {0:#,#00.00}%", status.MemoryUsingPer());
                Console.WriteLine();
                Console.WriteLine(status.GraphicName);
                Console.WriteLine("ビデオメモリ : " + status.TotalGraphicMemory.ToString() + "MB");
                Console.WriteLine("使用率 : {0:f}%", status.GraphicCoreUsing);
                Console.WriteLine("温度 : {0:f}℃", status.GraphicTemp);
                Console.WriteLine("使用メモリ : " + status.UsedGraphicMemory + "MB");
                Console.WriteLine("空きメモリ : " + status.FreeGraphicMemory + "MB");
                Console.WriteLine("メモリ使用率 : {0:#,#00.00}%", status.GraphicMemoryUsingPer());


                Thread.Sleep(1000);
                //Console.Clear();
                Console.CursorTop = 0;
                Console.CursorLeft = 0;
                
            }
            status.Close();
        }

    }

    class ComputerStatus
    {
        private readonly Computer computer;

        //CPU
        public string CpuName { get; }                                  //CPU名
        public int CoreCount { get; }                                   //物理コア数
        public int HtCoreCount { get; }                                 //スレッド数
        public double[] CoreTemps { get; private set; }                 //物理コア温度
        public double PackageTemp { get; private set; }                 //パッケージ温度
        public double CpuTemp { get; private set; }                     //CPU平均温度
        public double[] HtCoreUsings { get; private set; }              //スレッド使用率
        public double CpuUsing { get; private set; }                    //CPU全体使用率

        //メインメモリ
        public double TotalMemory { get; }                              //全体メモリ量
        public double UsedMemory { get; private set; }                  //使用メモリ量
        public double FreeMemory { get; private set; }                  //未使用メモリ量

        //グラボ
        public string GraphicName { get; }                              //グラフィック名
        public double TotalGraphicMemory { get; }                       //全体メモリ量
        public double UsedGraphicMemory { get; private set; }           //使用メモリ量
        public double FreeGraphicMemory { get; private set; }           //未使用メモリ量
        public double GraphicTemp { get; private set; }                 //コア温度
        public double GraphicCoreUsing { get; private set; }            //使用率

        public ComputerStatus()
        {
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsStorageEnabled = false,
                IsBatteryEnabled = false,
                IsPsuEnabled = false
            };
            computer.Open();
            computer.Accept(new UpdateVisitor());

            CpuName = computer.SMBios.Processors[0].Version.ToString(); //CPU名取得
            CoreCount = computer.SMBios.Processors[0].CoreCount;        //物理コア数取得
            HtCoreCount = computer.SMBios.Processors[0].ThreadCount;    //スレッド数取得
            CoreTemps = new double[CoreCount];                          //コア温度配列初期化
            HtCoreUsings = new double[HtCoreCount];                     //スレッド使用率配列初期化

            TotalMemory = 0;
            for (int i = 0; i < computer.SMBios.MemoryDevices.Length; i++)
            {
                TotalMemory += computer.SMBios.MemoryDevices[i].Size;
            }
            TotalMemory /= 1024.0;                                        //全体メモリ量取得

            foreach (IHardware hardware in computer.Hardware)
            {
                switch (hardware.HardwareType)
                {
                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        GraphicName = hardware.Name;    //GPU名取得
                        foreach(ISensor sensor in hardware.Sensors)
                        {
                            if(sensor.SensorType == SensorType.SmallData)
                            {
                                if(sensor.Name == "GPU Memory Total")
                                {
                                    TotalGraphicMemory = (double)(sensor.Value);    //GPU全体メモリ量取得
                                }
                            }
                        }
                        break;
                    default:
                        break;
                }
                hardware.Update();
            }
        }
        public void Update()
        {
            foreach (IHardware hardware in computer.Hardware)
            {
                switch (hardware.HardwareType)
                {
                    //メモリ取得系
                    case HardwareType.Memory:
                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.Name == "Memory Used")
                            {
                                UsedMemory = (double)sensor.Value;                          //使用メモリ量取得
                            }
                            else if (sensor.Name == "Memory Available")
                            {
                                FreeMemory = (double)sensor.Value;                          //空きメモリ量取得
                            }
                        }
                        break;
                    //CPU取得系
                    case HardwareType.Cpu:
                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                if (sensor.Index < CoreCount)
                                {
                                    CoreTemps[sensor.Index] = (double)sensor.Value;         //物理コア温度取得
                                }
                                else if (sensor.Name == "CPU Package")
                                {
                                    PackageTemp = (double)sensor.Value;                     //パッケージ温度取得
                                }
                                else if (sensor.Name == "Core Average")
                                {
                                    CpuTemp = (double)sensor.Value;                         //CPU平均温度取得
                                }
                            }
                            else if (sensor.SensorType == SensorType.Load)
                            {
                                if (sensor.Index > 1 & sensor.Index < HtCoreCount + 2)
                                {
                                    HtCoreUsings[sensor.Index - 2] = (double)sensor.Value;  //スレッド使用率取得
                                }
                                else if (sensor.Index == 0)
                                {
                                    CpuUsing = (double)sensor.Value;                        //平均CPU使用率取得
                                }
                            }
                        }
                        break;
                    //GPU取得系
                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                if (sensor.Index == 0)
                                {
                                    GraphicTemp = (double)sensor.Value;                     //コア温度取得
                                }
                            }
                            else if (sensor.SensorType == SensorType.SmallData)
                            {
                                if (sensor.Name == "GPU Memory Free")
                                {
                                    FreeGraphicMemory = (double)sensor.Value;               //未使用メモリ量取得
                                }
                                else if (sensor.Name == "GPU Memory Used")
                                {
                                    UsedGraphicMemory = (double)sensor.Value;               //使用メモリ量取得
                                }
                            }
                            else if (sensor.SensorType == SensorType.Load)
                            {
                                if (sensor.Name == "GPU Core")
                                {
                                    GraphicCoreUsing = (double)sensor.Value;                //使用率取得
                                }
                            }
                        }
                        break;
                    default:
                        break;
                }
                hardware.Update();
            }
        }
        public void Close()
        {
            computer.Close();
        }

        //メモリ使用率
        public double MemoryUsingPer()
        {
            return UsedMemory / TotalMemory * 100;
        }
        //グラボメモリ使用率
        public double GraphicMemoryUsingPer()
        {
            return UsedGraphicMemory / TotalGraphicMemory * 100;
        }

        private class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer)
            {
                computer.Traverse(this);
            }
            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
            }
            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }

    }
}
