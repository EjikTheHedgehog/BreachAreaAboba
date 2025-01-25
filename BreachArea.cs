using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ExileCore2;
using ExileCore2.PoEMemory.MemoryObjects;
using Positioned = ExileCore2.PoEMemory.Components.Positioned;
using ImGuiNET;

namespace BreachArea
{
    public class BreachArea : BaseSettingsPlugin<BreachAreaSettings>
    {
        private const string BREACH_PATH = "Metadata/MiscellaneousObjects/Breach/BreachObject";
        private readonly Dictionary<uint, BreachInfo> _breachCache = new();
        private readonly List<(uint EntityId, Vector2 Position, DateTime StartTime)> _transitionedBreaches = new();
        private readonly HashSet<uint> _processedTransitionIds = new();

        public override bool Initialise() => true;

        public override void AreaChange(AreaInstance area)
        {
            _breachCache.Clear();
            _transitionedBreaches.Clear();
            _processedTransitionIds.Clear();
        }

        public override void Tick()
        {
            var currentTime = DateTime.Now;
            
            foreach (var breach in _breachCache.Values.ToList())
            {
                var entity = breach.Entity;
                if (entity == null) continue;
                
                var id = entity.Id;
                var isTransitioned = entity.IsTransitioned;
                
                if (isTransitioned && !_transitionedBreaches.Any(b => b.EntityId == id))
                {
                    try 
                    {
                        _transitionedBreaches.Add((id, breach.Position, currentTime));
                        _breachCache.Remove(id);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to process breach transition: {ex.Message}");
                    }
                }
            }

            var breachEntities = GameController.EntityListWrapper.Entities
                .Where(e => e != null && 
                       e.Path?.Contains(BREACH_PATH) == true &&
                       !_transitionedBreaches.Any(b => b.EntityId == e.Id) &&
                       !_breachCache.Values.Any(b => b.Entity.Id == e.Id));

            foreach (var entity in breachEntities)
            {
                var pos = entity.GetComponent<Positioned>();
                if (pos != null)
                {
                    var position = new Vector2(pos.GridPosition.X, pos.GridPosition.Y);
                    _breachCache[entity.Id] = new BreachInfo(entity, position);
                }
            }
        }

        public override void Render()
        {
            if (Settings.DebugMenu.ShowDebugMenu)
            {
                RenderDebugMenu();
            }

            if (!_breachCache.Any() && !_transitionedBreaches.Any()) return;

            var player = GameController.Game.IngameState.Data.LocalPlayer;
            var playerPos = player?.GetComponent<Positioned>()?.GridPosition ?? Vector2.Zero;

            var sortedBreachCache = _breachCache.Values
                .OrderBy(b => Vector2.Distance(b.Position, playerPos))
                .Take(Settings.RenderSettings.HowManyBreachesToRender.Value);

            var sortedTransitionedBreaches = _transitionedBreaches
                .OrderBy(b => Vector2.Distance(b.Position, playerPos))
                .Take(Settings.RenderSettings.HowManyBreachesToRender.Value);

            if (Settings.RenderSettings.RenderStaticRange)
            {
                foreach (var breach in sortedBreachCache)
                {
                    DrawCircleOnLargeMap(
                        breach.Position,
                        Settings.DebugMenu.SpecificRange.Value,
                        Settings.RenderSettings.StaticBreachColor.Value,
                        Settings.RenderSettings.CirclesThickness.Value
                    );
                }

                if (Settings.RenderSettings.HideStaticRangeAfterClear)
                {
                    foreach (var (_, position, startTime) in sortedTransitionedBreaches)
                    {
                        var timeElapsed = (DateTime.Now - startTime).TotalSeconds;
                        if (timeElapsed < Settings.DebugMenu.SpecificDuration.Value)
                        {
                            DrawCircleOnLargeMap(
                                position,
                                Settings.DebugMenu.SpecificRange.Value,
                                Settings.RenderSettings.StaticBreachColor.Value,
                                Settings.RenderSettings.CirclesThickness.Value
                            );
                        }
                    }
                }
                else
                {
                    foreach (var (_, position, _) in sortedTransitionedBreaches)
                    {
                        DrawCircleOnLargeMap(
                            position,
                            Settings.DebugMenu.SpecificRange.Value,
                            Settings.RenderSettings.StaticBreachColor.Value,
                            Settings.RenderSettings.CirclesThickness.Value
                        );
                    }
                }
            }

            if (Settings.RenderSettings.RenderActiveRange)
            {
                foreach (var (_, position, startTime) in sortedTransitionedBreaches)
                {
                    var timeElapsed = (DateTime.Now - startTime).TotalSeconds;
                    
                    if (timeElapsed >= Settings.DebugMenu.SpecificDuration.Value)
                        continue;

                    DrawCircleOnLargeMap(
                        position,
                        Settings.DebugMenu.SpecificRange.Value,
                        Settings.RenderSettings.ActiveBreachColor.Value,
                        Settings.RenderSettings.CirclesThickness.Value,
                        startTime
                    );
                }
            }
        }

        private void RenderDebugMenu()
        {
            ImGui.SetNextWindowSize(new Vector2(400, 200), ImGuiCond.FirstUseEver);
            var isWindowOpen = Settings.DebugMenu.ShowDebugMenu.Value;
            if (ImGui.Begin("Breach Debug", ref isWindowOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                Settings.DebugMenu.ShowDebugMenu.Value = isWindowOpen;

                ImGui.Text($"Active Breaches: {_breachCache.Count}");
                ImGui.Text($"Transitioned Breaches: {_transitionedBreaches.Count}");
                
                if (_breachCache.Any())
                {
                    ImGui.Separator();
                    ImGui.Text("Active Breach Details:");
                    foreach (var breach in _breachCache.Values)
                    {
                        ImGui.Text($"Position: ({breach.Position.X:F1}, {breach.Position.Y:F1})");
                        ImGui.Text($"Is Transitioned: {breach.IsTransitioned}");
                        ImGui.Separator();
                    }
                }

                if (_transitionedBreaches.Any())
                {
                    ImGui.Separator();
                    ImGui.Text("Transitioned Breach Details:");
                    foreach (var (id, pos, startTime) in _transitionedBreaches)
                    {
                        var timeElapsed = (DateTime.Now - startTime).TotalSeconds;
                        ImGui.Text($"ID: {id}");
                        ImGui.Text($"Position: ({pos.X:F1}, {pos.Y:F1})");
                        ImGui.Text($"Time Elapsed: {timeElapsed:F1}s");
                        ImGui.Separator();
                    }
                }

                ImGui.Separator();
                ImGui.Text("Current Settings:");
                ImGui.Text($"Specific Duration: {Settings.DebugMenu.SpecificDuration.Value}s");
                ImGui.Text($"Specific Range: {Settings.DebugMenu.SpecificRange.Value}");

                ImGui.End();
            }
        }

        private float EaseOutQuad(float x) => 1 - (1 - x) * (1 - x);

        public void DrawCircleOnLargeMap(Vector2 position, float radius, System.Drawing.Color color, int thickness = 2, DateTime? startTime = null)
        {
            var largeMap = GameController.Game.IngameState.IngameUi.Map?.LargeMap?.AsObject<ExileCore2.PoEMemory.Elements.SubMap>();
            if (largeMap == null || !largeMap.IsVisible) return;

            if (startTime.HasValue)
            {
                var timeSinceStart = (float)(DateTime.Now - startTime.Value).TotalSeconds;
                var duration = Settings.DebugMenu.SpecificDuration.Value;
                var maxRadius = Settings.DebugMenu.SpecificRange.Value;
                
                var progress = Math.Min(timeSinceStart / duration, 1.0f);
                var easedProgress = EaseOutQuad(progress);
                radius = maxRadius * easedProgress;
            }

            try
            {
                Graphics.DrawCircleOnLargeMap(position, false, radius, color, thickness, 64);
            }
            catch (Exception ex)
            {
                LogError($"Error drawing circle on map: {ex.Message}");
            }
        }
    }

    internal class BreachInfo
    {
        public Vector2 Position { get; set; }
        public Entity Entity { get; set; }
        public bool IsTransitioned => Entity?.IsTransitioned ?? false;

        public BreachInfo(Entity entity, Vector2 position)
        {
            Entity = entity;
            Position = position;
        }

        public float CalculateDistance(Vector2 playerPosition)
        {
            return Vector2.Distance(Position, playerPosition);
        }
    }
}