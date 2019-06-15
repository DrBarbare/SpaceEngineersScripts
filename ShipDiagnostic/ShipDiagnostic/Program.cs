using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        const int kBlockScanLimit = 500;

        const bool kAppend = true;
        List<IMyTerminalBlock> lcds_;
        List<IMyTerminalBlock> blocks_;
        List<Vector3I> blocksCoordinates_;
        HashSet<int> visited_;

        IMyCubeGrid shipGrid_;
        IEnumerator<int> scanState_;
        int volume_;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;

            lcds_ = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName("[status]", lcds_, block => block is IMyTextPanel);

            blocks_ = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(blocks_);

            blocksCoordinates_ = new List<Vector3I>();
            visited_ = new HashSet<int>();
            shipGrid_ = Me.CubeGrid;
            volume_ = 0;
        }

        private IEnumerator<Vector3I> VolumeScan()
        {
            Vector3I vmin_ = shipGrid_.Min;
            Vector3I vmax_ = shipGrid_.Max;
            int dx_ = Math.Abs(vmax_.X - vmin_.X);
            int dy_ = Math.Abs(vmax_.Y - vmin_.Y);
            int dz_ = Math.Abs(vmax_.Z - vmin_.Z);
            volume_ = dx_ * dy_ * dz_;

            for (int z = 0; z < dz_; z += 1)
            {
                for (int y = 0; y < dy_; y += 1)
                {
                    for (int x = 0; x < dx_; x += 1)
                    {
                        yield return vmin_ + new Vector3I(x, y, z);
                    }
                }
            }
            yield return new Vector3I(0, 0, 0);
        }

        private IEnumerator<int> InitialScanImpl()
        {
            int totalCount = 0;
            int count = 0;

            using (var positions = VolumeScan())
            {
                while (positions.MoveNext())
                {
                    var position = positions.Current;
                    if (shipGrid_.CubeExists(position))
                    {
                        bool add = true;
                        var block = shipGrid_.GetCubeBlock(position);
                        // Is terminal block
                        if (block != null)
                        {
                            int hash = block.GetHashCode();
                            if (add = !visited_.Contains(hash))
                            {
                                visited_.Add(hash);
                            }
                        }

                        if (add)
                        {
                            blocksCoordinates_.Add(position);
                        }
                    }

                    count += 1;
                    if (count == kBlockScanLimit)
                    {
                        totalCount += count * 100;
                        yield return (totalCount / volume_);
                        count = 0;
                    }
                }
            }
            if (blocks_.Count != visited_.Count)
            {
                throw new Exception($"Didn't count all the terminal blocks b:{blocks_.Count} / v:{visited_.Count}");
            }
        }

        private int Scan()
        {
            if (scanState_ == null)
            {
                scanState_ = InitialScanImpl();
            }
            else if (!scanState_.MoveNext())
            {
                scanState_.Dispose();
            }

            return (scanState_ == null) ? 100 : scanState_.Current;
        }

        private void LCDPrint(String msg, bool append = false)
        {
            foreach (var block in lcds_)
            {
                var lcd = block as IMyTextPanel;
                lcd.WriteText(msg, append);
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            var name = shipGrid_.CustomName;

            int progress = Scan();

            LCDPrint($"{name} status:\nScan {progress}%\nBlocks counted {blocksCoordinates_.Count}");
        }
    }
}
