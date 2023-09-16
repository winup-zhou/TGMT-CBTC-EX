using AtsEx.PluginHost;
using AtsEx.PluginHost.Handles;
using AtsEx.PluginHost.Input.Native;
using AtsEx.PluginHost.Panels.Native;
using AtsEx.PluginHost.Plugins;
using AtsEx.PluginHost.Sound;
using AtsEx.PluginHost.Sound.Native;
using BveTypes.ClassWrappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TGMTAts {
    [PluginType(PluginType.VehiclePlugin)]
    public partial class TGMTAts : AssemblyPluginBase {
        internal static SpeedLimit nextLimit;

        public static SpeedLimit movementEndpoint = SpeedLimit.inf;

        public static int pPower, pBrake;
        public static ReverserPosition pReverser;

        private readonly IAtsSound atsSound0, atsSound1, atsSound2;
        private readonly IAtsPanelValue<int> atsPanel36, atsPanel40, atsPanel41;
        public TGMTAts(PluginBuilder services) : base(services) { 
            Load();
            ;
            atsSound0 = Native.AtsSounds.Register(0);
            atsSound1 = Native.AtsSounds.Register(1);
            atsSound2 = Native.AtsSounds.Register(2);

            atsPanel36 = Native.AtsPanelValues.RegisterInt32(36);
            atsPanel40 = Native.AtsPanelValues.RegisterInt32(40);
            atsPanel41 = Native.AtsPanelValues.RegisterInt32(41);

            Native.NativeKeys.AtsKeys[NativeAtsKeyName.A1].Pressed += OnA1Pressed;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.B1].Pressed += OnB1Pressed;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.B2].Pressed += OnB2Pressed;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.C1].Pressed += OnC1Pressed;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.C2].Pressed += OnC2Pressed;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.A1].Released += OnA1Up;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.B1].Released += OnB1Up;

            Native.BeaconPassed += SetBeaconData;
            Native.DoorClosed += DoorClose;
            Native.DoorOpened += DoorOpen;
            Native.Started += Initialize;

            vehicleSpec = Native.VehicleSpec;
        }

        public override TickResult Tick(TimeSpan elapsed) {
            var state = Native.VehicleState;

            SpeedLimit nextLimit;

            SpeedLimit movementEndpoint = SpeedLimit.inf;

            int speedMultiplier = 4;

            AtsEx.PluginHost.Handles.HandleSet handles = Native.Handles;
            VehiclePluginTickResult tickResult = new VehiclePluginTickResult();

            pluginReady = true;
            ackMessage = 0;
            location = state.Location;
            time = state.Time.TotalMilliseconds;

            pPower = handles.Power.Notch;
            pBrake = handles.Brake.Notch;
            pReverser = handles.Reverser.Position;

            int pCommand = 0, bCommand = 0;
            ReverserPosition rCommand = 0;

            double ebSpeed = 0, recommendSpeed = 0, targetSpeed = 0, targetDistance = 0;
            trackLimit.Update(location);
            StationManager.Update(state, doorOpen);

            CalculatedLimit maximumCurve = null, targetCurve = null, recommendCurve = null;
            switch (signalMode) {
                case 0:
                    ebSpeed = Config.RMSpeed;
                    recommendSpeed = -10;
                    targetDistance = -10;
                    targetSpeed = -10;
                    driveMode = 0;
                    break;
                case 1:
                    // ITC
                    if (selectedMode > 0 && driveMode == 0) driveMode = 1;
                    maximumCurve = CalculatedLimit.Calculate(location,
                        Config.EbPatternDeceleration, Config.RecommendSpeedOffset, movementEndpoint, trackLimit);
                    targetCurve = CalculatedLimit.Calculate(location,
                        Config.EbPatternDeceleration, 0, movementEndpoint, trackLimit);
                    recommendCurve = CalculatedLimit.Calculate(location,
                        Config.RecommendDeceleration, 0, StationManager.RecommendCurve(), movementEndpoint, trackLimit);
                    // 释放速度
                    if (movementEndpoint.Location - location < Config.ReleaseSpeedDistance
                        && movementEndpoint.Location > location
                        && state.Speed < Config.ReleaseSpeed && !releaseSpeed) {
                        ackMessage = 2;
                    }
                    break;
                case 2:
                    // CTC
                    releaseSpeed = false;
                    movementEndpoint = StationManager.CTCEndpoint();
                    if (selectedMode > 0 && driveMode == 0) driveMode = 1;
                    maximumCurve = CalculatedLimit.Calculate(location,
                        Config.EbPatternDeceleration, Config.RecommendSpeedOffset, movementEndpoint,
                        PreTrainManager.GetEndpoint(), trackLimit);
                    targetCurve = CalculatedLimit.Calculate(location,
                        Config.EbPatternDeceleration, 0, movementEndpoint,
                        PreTrainManager.GetEndpoint(), trackLimit);
                    recommendCurve = CalculatedLimit.Calculate(location,
                        Config.RecommendDeceleration, 0, StationManager.RecommendCurve(),
                        PreTrainManager.GetEndpoint(), movementEndpoint, trackLimit);
                    break;
                default:
                    // fallback
                    ebSpeed = Config.MaxSpeed;
                    recommendSpeed = -10;
                    targetSpeed = 0;
                    targetDistance = -10;
                    break;
            }
            if (maximumCurve != null) {
                // ITC/CTC 有速度曲线
                ebSpeed = Math.Min(Config.MaxSpeed, Math.Max(0, maximumCurve.CurrentTarget));
                recommendSpeed = Math.Min(ebSpeed - Config.RecommendSpeedOffset,
                    Math.Max(0, recommendCurve.CurrentTarget));
                nextLimit = targetCurve.NextLimit;
                targetDistance = targetCurve.NextLimit.Location - location;
                targetSpeed = targetCurve.NextLimit.Limit;
                if (location > movementEndpoint.Location) {
                    // 如果已冲出移动授权终点，释放速度无效
                    if (releaseSpeed) Log("超出了移动授权终点, 释放速度无效");
                    recommendSpeed = 0;
                    ebSpeed = 0;
                    releaseSpeed = false;
                }
                if (releaseSpeed) {
                    ebSpeed = Math.Max(ebSpeed, Config.ReleaseSpeed);
                    recommendSpeed = Math.Max(recommendSpeed, Config.ReleaseSpeed - Config.RecommendSpeedOffset);
                }
            }

            // 显示速度、预选模式、驾驶模式、控制级别、车门模式
            panel_[1] = Convert.ToInt32(Math.Ceiling(Math.Abs(state.Speed) * speedMultiplier));
            panel_[22] = selectedMode;
            panel_[24] = driveMode;
            panel_[25] = signalMode;
            panel_[28] = (driveMode > 0) ? (driveMode > 1 ? doorMode : 1) : 0;

            // 显示临时预选模式
            if (state.Speed != 0 || time > selectModeStartTime + Config.ModeSelectTimeout * 1000) {
                selectingMode = -1;
                selectModeStartTime = 0;
            }
            if (selectingMode >= 0) {
                ackMessage = 4;
                panel_[22] = time % 500 < 250 ? selectingMode : 6;
            }

            // 显示目标速度、建议速度、干预速度
            if (signalMode > 1 && state.Speed == 0 &&
                Math.Abs(StationManager.NextStation.StopPosition - location) < Config.DoorEnableWindow
                && time < StationManager.NextStation.RouteOpenTime) {
                targetDistance = 0;
                targetSpeed = -10;
                recommendSpeed = ebSpeed = 0;
            }

            if (doorOpen) {
                targetDistance = 0;
                targetSpeed = -10;
            }
            panel_[11] = distanceToPixel(targetDistance);
            panel_[19] = (int)targetDistance;
            panel_[16] = (int)(ebSpeed * speedMultiplier);
            if (driveMode < 2) {
                panel_[15] = (int)(recommendSpeed * speedMultiplier);
            } else {
                panel_[15] = -1;
            }
            distanceToColor(targetSpeed, targetDistance);
            targetSpeed = Math.Min(targetSpeed, Config.MaxSpeed);
            panel_[17] = (int)targetSpeed;
            panel_[18] = (targetSpeed < 0) ? 1 : 0;
            panel_[29] = panel_[31] = 0;

            // 显示出发与屏蔽门信息
            if (signalMode > 1 && state.Speed == 0) {
                if (Math.Abs(StationManager.NextStation.StopPosition - location) < Config.DoorEnableWindow
                    && time > StationManager.NextStation.DepartureTime - Config.DepartRequestTime * 1000 && !doorOpen && StationManager.Arrived
                    && time >= StationManager.NextStation.RouteOpenTime) {
                    panel_[32] = 2;
                } else if (Math.Abs(StationManager.NextStation.StopPosition - location) < Config.DoorEnableWindow
                    && time - doorOpenTime >= Config.CloseRequestShowTime * 1000 && doorOpen && time > StationManager.NextStation.DepartureTime - (Config.DepartRequestTime + 20) * 1000
                    && StationManager.Arrived && time >= StationManager.NextStation.RouteOpenTime) {
                    panel_[32] = 1;
                    atsSound1.Play();
                } else if (Math.Abs(StationManager.NextStation.StopPosition - location) < Config.DoorEnableWindow
                    && time < StationManager.NextStation.RouteOpenTime) {
                    panel_[32] = 4;
                } else {
                    panel_[32] = 0;
                }
            } else {
                panel_[32] = 0;
            }
            if (signalMode >= 2 && state.Speed == 0) {
                if (doorOpen) {
                    if (time - doorOpenTime >= 1000) {
                        panel_[29] = 3;
                    } else {
                        panel_[29] = 0;
                    }
                } else {
                    if (time - doorCloseTime >= 1000) {
                        panel_[29] = 0;
                    } else {
                        panel_[29] = 3;
                    }
                }
            }

            // 如果没有无线电，显示无线电故障
            panel_[23] = state.Speed == 0 ? 0 : 1;
            panel_[30] = deviceCapability != 2 ? 1 : 0;

            // ATO
            atsPanel40.Value = 0;
            Ato.UpdateAccel(state.Speed, recommendSpeed);
            if (signalMode > 0) {
                if (handles.Power.Notch != 0 || handles.Brake.Notch != 0 || handles.Reverser.Position != ReverserPosition.F ) {
                    driveMode = 1;
                }
                if (recommendSpeed == 0 && state.Speed == 0) {
                    driveMode = 1;
                }
                if (driveMode >= 2) {
                    atsPanel40.Value = 1;
                    var notch = Ato.GetCmdNotch(state.Speed, recommendSpeed, ebSpeed);
                    if (notch < 0) {
                        pCommand = 0;
                        bCommand = Math.Min(-notch, handles.Brake.MaxServiceBrakeNotch);
                        panel_[21] = 3;
                    } else if (notch > 0) {
                        pCommand = Math.Min(notch, handles.Power.MaxPowerNotch);
                        bCommand = 0;
                        panel_[21] = 1;
                    } else {
                        pCommand = 0;
                        bCommand = 0;
                        panel_[21] = 2;
                    }
                } else {
                    panel_[21] = 0;
                    if (Ato.IsAvailable()) {
                        // 闪烁
                        atsPanel40.Value = time % 500 < 250 ? 1 : 0;
                    }
                }
            }

            // ATP 制动干预部分
            if (ebSpeed > 0) {
                // 有移动授权
                if (state.Speed == 0 && handles.Power.Notch == 0) {
                    // 低于制动缓解速度
                    if (ebState > 0) {
                        if (location > movementEndpoint.Location) {
                            // 冲出移动授权终点，要求RM
                            ackMessage = 6;
                        } else {
                            bCommand = 0;
                            ebState = 0;
                        }
                    }
                    panel_[10] = 0;
                } else if (state.Speed > ebSpeed) {
                    // 超出制动干预速度
                    ebState = 1;
                    if (driveMode > 1) driveMode = 1;
                    atsSound0.Play();
                    panel_[10] = 2;
                    panel_[29] = 2;
                    bCommand = Math.Max(bCommand, handles.Brake.EmergencyBrakeNotch);
                } else {
                    if (ebState > 0) {
                        // 刚刚触发紧急制动，继续制动
                        panel_[10] = 2;
                        panel_[29] = 2;
                        bCommand = Math.Max(bCommand, handles.Brake.EmergencyBrakeNotch);
                    } else if (driveMode == 1 && state.Speed > recommendSpeed) {
                        // 超出建议速度，显示警告
                        if (panel_[10] == 0) atsSound2.Play();
                        panel_[10] = 1;
                    } else {
                        panel_[10] = 0;
                    }
                }
            } else if (signalMode == 1) {
                // ITC下冲出移动授权终点。
                if (state.Speed == 0) {
                    // 停稳后降级到RM模式。等待确认。
                    ackMessage = 6;
                }
                ebState = 1;
                // 显示紧急制动、目标距离0、速度0
                panel_[10] = 2;
                panel_[29] = 2;
                panel_[11] = 0;
                panel_[19] = 0;
                panel_[17] = 0;
                bCommand = Math.Max(bCommand, handles.Brake.EmergencyBrakeNotch);
            }

            // 防溜、车门零速保护
            if (state.Speed < 0.5 && handles.Power.Notch < 1 && handles.Brake.Notch < 1 && driveMode != 2) {
                bCommand = Math.Min(Math.Max(bCommand, 1), handles.Brake.MaxServiceBrakeNotch);
            }
            if (doorOpen) {
                panel_[15] = -10 * speedMultiplier;
                panel_[16] = 0;
                if (handles.Brake.Notch < 4) bCommand = Math.Min(Math.Max(bCommand, 1), handles.Brake.MaxServiceBrakeNotch);
            }

            // 后退监督: 每1m一次紧制 (先这么做着, 有些地区似乎是先1m之后每次0.5m)
            if (handles.Reverser.Position == ReverserPosition.B) {
                if (location > reverseStartLocation) reverseStartLocation = location;
                if (location < reverseStartLocation - Config.ReverseStepDistance) {
                    if (state.Speed == 0 && handles.Power.Notch == 0) {
                        reverseStartLocation = location;
                    } else {
                        bCommand = Math.Max(bCommand, handles.Brake.EmergencyBrakeNotch);
                    }
                }
            } else if (state.Speed >= 0) {
                reverseStartLocation = Config.LessInf;
            }

            // 显示释放速度、确认消息
            if (releaseSpeed) panel_[31] = 3;
            if (ackMessage > 0) {
                panel_[35] = ackMessage;
                panel_[36] = atsPanel36.Value = ((state.Time.TotalMilliseconds / 1000) % 0.5 < 0.25) ? 1 : 0;
            } else {
                panel_[35] = panel_[36] = atsPanel36.Value = 0;
            }

            // 显示TDT、车门使能，车门零速保护
            if (StationManager.NextStation != null) {
                int sectogo = Convert.ToInt32((state.Time.TotalMilliseconds / 1000) - StationManager.NextStation.DepartureTime);
                if (StationManager.Arrived) {
                    // 已停稳，可开始显示TDT
                    if (location - StationManager.NextStation.StopPosition < Config.TDTFreezeDistance) {
                        // 未发车
                        // 这里先要求至少100m的移动授权
                        if (movementEndpoint.Location - location > 100) {
                            // 出站信号绿灯
                            if (sectogo < 0) {
                                // 未到发车时间
                                panel_[102] = -1;
                            } else {
                                panel_[102] = 1;
                            }
                        } else {
                            // 出站信号红灯
                            panel_[102] = -1;
                        }
                        if (sectogo < 0) {
                            // 未到发车时间
                            panel_[102] = -1;
                        } else {
                            panel_[102] = 1;
                        }
                        panel_[101] = Math.Min(Math.Abs(sectogo), 999);
                    } else {
                        // 已发车
                        panel_[102] = -1;
                    }
                } else {
                    panel_[102] = 0;
                    panel_[101] = 0;
                }
                if (StationManager.NextStation.DepartureTime < 0.1) panel_[102] = 0;
                if (Math.Abs(StationManager.NextStation.StopPosition - location) < Config.StationStartDistance) {
                    // 在车站范围内
                    if (Math.Abs(StationManager.NextStation.StopPosition - location) < Config.DoorEnableWindow) {
                        // 在停车窗口内
                        if (state.Speed < 1) {
                            panel_[26] = 2;
                        } else {
                            panel_[26] = 1;
                        }
                        if (state.Speed == 0) {
                            // 停稳, 可以解锁车门, 解锁对应方向车门
                            if (StationManager.NextStation.DoorOpenType == 1) {
                                panel_[27] = 1;
                            } else if (StationManager.NextStation.DoorOpenType == 2) {
                                panel_[27] = 3;
                            } else if (StationManager.NextStation.DoorOpenType == 3) {
                                panel_[27] = 7;
                            } else if (StationManager.NextStation.DoorOpenType == 4) {
                                panel_[27] = 5;
                            } else if (StationManager.NextStation.DoorOpenType == 5) {
                                panel_[27] = 9;
                            } else {
                                panel_[27] = 0;
                            }
                            if (doorOpen) {
                                panel_[27] = StationManager.NextStation.DoorOpenType == 0 ? 12 : panel_[27] + 1;
                            }
                        } else {
                            panel_[27] = 0;
                        }
                    } else {
                        // 不在停车窗口内
                        panel_[26] = 1;
                        panel_[27] = 0;
                    }
                } else {
                    // 不在车站范围内
                    panel_[26] = 0;
                    panel_[27] = doorOpen ? 11 : 0;
                }
                if (signalMode == 0) {
                    // RM-IXL, 门要是开了就当它按了门允许, 没有车门使能和停车窗口指示
                    panel_[26] = 0;
                    panel_[27] = doorOpen ? 12 : 0;
                }
            }

            if (StationManager.NextStation.Pass && Math.Abs(StationManager.NextStation.StopPosition - location) < Config.StationStartDistance + 200) panel_[32] = 3;

            // 信号灯
            if (signalMode >= 2) {
                atsPanel41.Value = 2;
            } else {
                if (doorOpen) {
                    if (time - doorOpenTime >= 1000) {
                        atsPanel41.Value = 1;
                    } else {
                        atsPanel41.Value = 0;
                    }
                } else {
                    if (time - doorCloseTime >= 1000) {
                        atsPanel41.Value = 0;
                    } else {
                        atsPanel41.Value = 1;
                    }
                }
            }


            NotchCommandBase powerCommand = handles.Power.GetCommandToSetNotchTo(Math.Max(pCommand, handles.Power.Notch));
            NotchCommandBase brakeCommand = handles.Brake.GetCommandToSetNotchTo(Math.Max(bCommand, handles.Brake.Notch));
            ReverserPositionCommandBase reverserCommand = ReverserPositionCommandBase.Continue;
            ConstantSpeedCommand? constantSpeedCommand = ConstantSpeedCommand.Continue;

            tickResult.HandleCommandSet = new HandleCommandSet(powerCommand, brakeCommand, reverserCommand, constantSpeedCommand);

            // 刷新HMI, TDT, 信号机材质，为了减少对FPS影响把它限制到最多一秒十次
            if (hHMITex.HasEnoughTimePassed(10)) {
                hHMITex.Update(TGMTPainter.PaintHMI(state));
                hTDTTex.Update(TGMTPainter.PaintTDT(state));
            }


            return tickResult;
        }
        // 把目标距离折算成距离条上的像素数量。
        public static int distanceToPixel(double targetdistance) {
            int tgpixel = -10;
            if (targetdistance < 1) {
                tgpixel = 0;
            } else if (targetdistance < 2) {
                tgpixel = Convert.ToInt32(0 + (targetdistance - 1) / 1 * 20);
            } else if (targetdistance < 5) {
                tgpixel = Convert.ToInt32(20 + (targetdistance - 2) / 3 * 30);
            } else if (targetdistance < 10) {
                tgpixel = Convert.ToInt32(50 + (targetdistance - 5) / 5 * 20);
            } else if (targetdistance < 20) {
                tgpixel = Convert.ToInt32(70 + (targetdistance - 10) / 10 * 20);
            } else if (targetdistance < 50) {
                tgpixel = Convert.ToInt32(90 + (targetdistance - 20) / 30 * 28);
            } else if (targetdistance < 100) {
                tgpixel = Convert.ToInt32(118 + (targetdistance - 50) / 50 * 20);
            } else if (targetdistance < 200) {
                tgpixel = Convert.ToInt32(138 + (targetdistance - 100) / 100 * 20);
            } else if (targetdistance < 500) {
                tgpixel = Convert.ToInt32(158 + (targetdistance - 200) / 300 * 27);
            } else if (targetdistance < 750) {
                tgpixel = Convert.ToInt32(185 + (targetdistance - 500) / 250 * 15);
            } else {
                tgpixel = 200;
            }
            return tgpixel;
        }

        // 根据把目标距离设定距离条的颜色。
        public static void distanceToColor(double targetspeed, double targetdistance) {
            if (targetspeed < 0) {
                panel_[12] = 0; panel_[13] = 0; panel_[14] = 0;
            } else if (targetspeed == 0) {
                if (targetdistance < 150) {
                    panel_[12] = 1; panel_[13] = 0; panel_[14] = 0;
                } else if (targetdistance < 300) {
                    panel_[12] = 0; panel_[13] = 1; panel_[14] = 0;
                } else {
                    panel_[12] = 0; panel_[13] = 0; panel_[14] = 1;
                }
            } else if (targetspeed <= 25) {
                if (targetdistance < 300) {
                    panel_[12] = 0; panel_[13] = 1; panel_[14] = 0;
                } else {
                    panel_[12] = 0; panel_[13] = 0; panel_[14] = 1;
                }
            } else if (targetspeed <= 60) {
                if (targetdistance < 150) {
                    panel_[12] = 0; panel_[13] = 1; panel_[14] = 0;
                } else {
                    panel_[12] = 0; panel_[13] = 0; panel_[14] = 1;
                }
            } else {
                panel_[12] = 0; panel_[13] = 0; panel_[14] = 1;
            }
        }

    }
}
