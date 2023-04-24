using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePD.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ParkingMeters
{
    internal class ParkingMeter : Plugin
    {
        private static readonly Random _rng = new Random();

        private List<Prop> _meters = new List<Prop>();
        private List<Prop> _recentMeters = new List<Prop>();

        private bool _isNearMeter = false;
        private Prop _nearestMeter = null;

        private bool _isOnDuty = false;
        private bool _startedMeters = false;
        internal ParkingMeter()
        {
            Init();
            Events.OnDutyStatusChange += OnDutyStatusChange;
        }
        private async Task OnDutyStatusChange(bool isOnDuty)
        {
            if (isOnDuty)
            {
                _isOnDuty = true;
            }
            else
            {
                _isOnDuty = false;
            }

            await Task.FromResult(0);
        }
        private void Init()
        {
            API.RegisterCommand("StartMeters", new Action(() => {

                if (!_isOnDuty) { ShowNotification("Must be on duty"); return; }
                if (_startedMeters) { return; }

                _startedMeters = true;

                Tick += CheckIfNearMeter;
                Tick += DrawTextNearMeter;
                Tick += CatalogueMeters;
                Tick += ResetMeters;

                ShowNotification("Meter Maid ~g~started");

            }), false);

            API.RegisterCommand("StopMeters", new Action(() => {

                if (!_startedMeters) { return; }

                _startedMeters = false;

                Tick -= CheckIfNearMeter;
                Tick -= DrawTextNearMeter;
                Tick -= CatalogueMeters;
                Tick -= ResetMeters;

                ShowNotification("Meter Miad ~r~ended");
            }), false);

            TriggerEvent("chat:addSuggestion", "/StartMeters", "Begin your shift as a Meter Maid");
            TriggerEvent("chat:addSuggestion", "/StopMeters", "End your shift as a Meter Maid");
        }
        private async Task CatalogueMeters()
        {
            await Delay(100);
            var props = World.GetAllProps().Where(p => p.Model.Hash == API.GetHashKey("prop_parknmeter_01") || p.Model.Hash == API.GetHashKey("prop_parknmeter_02"));

            foreach (Prop prop in props)
            {
                //Make sure its not in either list of meters
                if (!_meters.Contains(prop) && !_recentMeters.Contains(prop))
                {
                    _meters.Add(prop);
                }
            }
        }
        private async Task CheckIfNearMeter()
        {
            if (_meters.Count == 0) { return; }

            foreach(Prop meter in _meters)
            {
                if(World.GetDistance(Game.PlayerPed.Position, meter.Position) < 1.5f) 
                {
                    _isNearMeter = true;
                    _nearestMeter = meter;
                }
            }

            await Task.FromResult(0);
        }
        private async Task DrawTextNearMeter()
        {
            if(!_isNearMeter || _nearestMeter == null) { return; }

            if(World.GetDistance(Game.PlayerPed.Position, _nearestMeter.Position) > 2f) { return; }

            Draw3dText("Press ~r~[H]~s~ to ~y~Check Meter", new Vector3(_nearestMeter.Position.X, _nearestMeter.Position.Y, _nearestMeter.Position.Z + 1f));

            if(Game.IsControlJustPressed(0, (Control)74))
            {
                var vehs = World.GetAllVehicles().Where(v => World.GetDistance(_nearestMeter.Position, v.Position) <= 2.5f);
                if(vehs.Count() <= 0) { ShowNotification("No Vehicle at this ~y~Meter"); _nearestMeter = new Prop(0); _isNearMeter = false; return; }

                API.TaskStartScenarioInPlace(Game.PlayerPed.Handle, "PROP_HUMAN_PARKING_METER", 0, true);
                await Delay(250);

                while(API.IsPedUsingAnyScenario(Game.PlayerPed.Handle)) { await Delay(10); }

                if (_rng.Next(0,100) <= 32)
                {
                    ShowNotification($"~y~Meter~s~ has ~r~{_rng.Next(10,25)}~s~ Minutes Remaining");
                }
                else
                {
                    ShowNotification("Meter has ~r~expired~s~");
                }

                _meters.Remove(_nearestMeter);
                _recentMeters.Add(_nearestMeter);

                _nearestMeter = new Prop(0);
                _isNearMeter = false;
            }

            await Task.FromResult(0);
        }
        private async Task ResetMeters()
        {
            //5 minute delay
            await Delay(300000);

            if(_recentMeters.Count == 0) { return; }

            for(int i = 0; i < _rng.Next(1,5); i++)
            {
                try
                {
                    Prop meter = _recentMeters.First();

                    _recentMeters.Remove(meter);
                    _meters.Add(meter);
                }
                catch
                {
                    //I assume there will be an index out of bounds error if someone only gets 1 meter and the _rng rolls 3 or something. Just let things carry on as normal.
                }
            }

            ShowNotification("Some ~y~meters~s~ have reset");
            ShowSubtitle("Some of the ~y~meters~s~ you checked before can be checked again", 6500);
        }
        private void ShowSubtitle(string msg, int duration)
        {
            API.BeginTextCommandPrint("STRING");
            API.AddTextComponentString(msg);
            API.EndTextCommandPrint(duration, false);
        }
        private void ShowNotification(string msg)
        {
            API.SetNotificationTextEntry("STRING");
            API.AddTextComponentString(msg);
            API.DrawNotification(true, true);
        }
        private void Draw3dText(string msg, Vector3 pos)
        {
            float textX = 0f, textY = 0f;
            Vector3 camLoc;
            API.World3dToScreen2d(pos.X, pos.Y, pos.Z, ref textX, ref textY);
            camLoc = API.GetGameplayCamCoords();
            float distance = API.GetDistanceBetweenCoords(camLoc.X, camLoc.Y, camLoc.Z, pos.X, pos.Y, pos.Z, true);
            float scale = (1 / distance) * 2;
            float fov = (1 / API.GetGameplayCamFov()) * 100;
            scale = scale * fov * 0.5f;

            API.SetTextScale(0.0f, scale);
            API.SetTextFont(0);
            API.SetTextProportional(true);
            API.SetTextColour(255, 255, 255, 215);
            API.SetTextDropshadow(0, 0, 0, 0, 255);
            API.SetTextEdge(2, 0, 0, 0, 150);
            API.SetTextDropShadow();
            API.SetTextOutline();
            API.SetTextEntry("STRING");
            API.SetTextCentre(true);
            API.AddTextComponentString(msg);
            API.DrawText(textX, textY);
        }
    }
}
