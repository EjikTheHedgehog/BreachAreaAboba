using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using ExileCore2;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.MemoryObjects;
using ImGuiNET;
using Positioned = ExileCore2.PoEMemory.Components.Positioned;
namespace BreachArea;

public class BreachArea : BaseSettingsPlugin<BreachAreaSettings>
{
    private const double CameraAngle = 38.7 * Math.PI / 180;
    private static readonly float CameraAngleCos = (float)Math.Cos(CameraAngle);
    private static readonly float CameraAngleSin = (float)Math.Sin(CameraAngle);
    private const int TileToWorldConversion = 250;
    private const int TileToGridConversion = 23;
    public const float GridToWorldMultiplier = TileToWorldConversion / (float)TileToGridConversion;

    private float EaseOutQuad(float x)
    {
        return 1 - (1 - x) * (1 - x);
    }

    private class BreachInfo
    {
        public Vector2 Position { get; set; }
        public bool IsTransitioned { get; set; }
        public bool IsExpanding { get; set; }
        public Entity Entity { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime? ExpansionStartTime { get; set; }

        public BreachInfo(Entity entity, Vector2 position)
        {
            Entity = entity;
            Position = position;
            IsTransitioned = entity.IsTransitioned;
            IsExpanding = false;
            LastSeen = DateTime.Now;
            ExpansionStartTime = null;
        }

        public bool IsValid => 
            (Entity != null && Entity.Address != 0) || 
            (ExpansionStartTime != null && (DateTime.Now - ExpansionStartTime.Value).TotalSeconds <= TARGET_TIME);

        public void Update()
        {
            LastSeen = DateTime.Now;
            
            if (Entity != null && Entity.Address != 0)
            {
                if (Entity.IsTransitioned && !IsExpanding)
                {
                    IsExpanding = true;
                    ExpansionStartTime = DateTime.Now;
                }
                IsTransitioned = Entity.IsTransitioned;
            }
        }

        public bool ShouldShowExpansion => 
            IsExpanding && 
            ExpansionStartTime != null && 
            (DateTime.Now - ExpansionStartTime.Value).TotalSeconds <= TARGET_TIME;
    }

    private const string BREACH_PATH = "Metadata/MiscellaneousObjects/Breach/BreachObject";
    private float _currentRadius = 0f;
    private const float TARGET_TIME = 53f;
    private float FIXED_SPEED => (Settings.MaxCircleSize / TARGET_TIME) / 1000;

    private readonly Dictionary<long, BreachInfo> _breachCache = new();

    public override bool Initialise()
    {
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        _breachCache.Clear();
    }

    public override void Tick()
    {
        foreach (var breach in _breachCache.ToList())
        {
            if (!breach.Value.IsValid)
            {
                _breachCache.Remove(breach.Key);
                continue;
            }
            breach.Value.Update();
        }

        if (_breachCache.Values.Any(b => b.ShouldShowExpansion))
        {
            _currentRadius += FIXED_SPEED * (float)GameController.DeltaTime;
            if (_currentRadius > Settings.MaxCircleSize)
            {
                _currentRadius = 0f;
            }
        }

        var breachEntities = GameController.EntityListWrapper.Entities
            .Where(e => e != null && 
                   e.Address != 0 && 
                   e.Path?.Contains(BREACH_PATH) == true);

        foreach (var entity in breachEntities)
        {
            if (!_breachCache.ContainsKey(entity.Address))
            {
                var pos = entity.GetComponent<Positioned>();
                if (pos != null)
                {
                    var position = new Vector2(pos.GridPosition.X, pos.GridPosition.Y);
                    _breachCache[entity.Address] = new BreachInfo(entity, position);
                }
            }
        }
    }

    public override void Render()
    {
        ImGui.SetNextWindowPos(new Vector2(10, 8));
        ImGui.Begin("BreachInfo", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
        var transitionedCount = _breachCache.Values.Count(x => x.IsTransitioned);
        ImGui.Text($"Breaches: {_breachCache.Count} (Transitioned: {transitionedCount})");
        ImGui.End();

        foreach (var breach in _breachCache.Values.Where(b => b.ShouldShowExpansion))
        {
            var timeSinceStart = (float)(DateTime.Now - breach.ExpansionStartTime.Value).TotalSeconds;
            var timeLeft = Math.Max(0, TARGET_TIME - timeSinceStart);
            
            var windowSize = GameController.Window.GetWindowRectangle();
            var timerText = $"{timeLeft:F1}s";
            var textSize = Graphics.MeasureText(timerText, 72);
            var position = new Vector2(
                windowSize.Width / 2 - textSize.X / 2,
                50);

            Graphics.DrawText(timerText, position, Color.White);
            break;
        }

        var map = GameController.Game.IngameState.IngameUi.Map;
        var largeMap = map?.LargeMap?.AsObject<SubMap>();
        if (largeMap == null || !largeMap.IsVisible)
        {
            RenderBreachesOnScreen();
            return;
        }

        RenderBreachesOnMap(largeMap);
    }

    private void RenderBreachesOnScreen()
    {
        foreach (var breachInfo in _breachCache.Values)
        {
            var worldPos = new Vector3(
                breachInfo.Position.X * GridToWorldMultiplier,
                breachInfo.Position.Y * GridToWorldMultiplier,
                0
            );
            
            var screenPos = GameController.Game.IngameState.Camera.WorldToScreen(worldPos);

            try
            {
                Graphics.DrawCircle(screenPos, Settings.StaticCircleSize, Color.Purple, 2);
                
                if (breachInfo.IsTransitioned)
                {
                    Graphics.DrawCircle(screenPos, _currentRadius, Color.White, 2);
                }
            }
            catch (Exception ex)
            {
                //Aboba
            }
        }
    }

    private Vector2 TranslateGridDeltaToMapDelta(Vector2 delta, float deltaZ, double mapScale)
    {
        deltaZ /= GridToWorldMultiplier;
        return (float)mapScale * new Vector2(
            (delta.X - delta.Y) * CameraAngleCos,
            (deltaZ - (delta.X + delta.Y)) * CameraAngleSin
        );
    }

    private void RenderBreachesOnMap(SubMap largeMap)
    {
        var mapScale = largeMap.MapScale * Settings.CustomScale;
        var player = GameController.Game.IngameState.Data.LocalPlayer;
        var playerRender = player?.GetComponent<ExileCore2.PoEMemory.Components.Render>();
        
        if (playerRender == null) return;

        var playerPosition = player.GetComponent<Positioned>().GridPosition;
        var playerHeight = -playerRender.RenderStruct.Height;
        var mapCenter = largeMap.MapCenter;

        foreach (var breachInfo in _breachCache.Values)
        {
            var breachPos = breachInfo.Position;
            var posDelta = new Vector2(breachPos.X - playerPosition.X, breachPos.Y - playerPosition.Y);
            
            var mapDelta = TranslateGridDeltaToMapDelta(posDelta, playerHeight, mapScale);
            var finalMapPos = mapCenter + mapDelta;

            try
            {
                var staticRadius = Settings.StaticCircleSize * mapScale;
                Graphics.DrawCircleOnLargeMap(new Vector2(breachPos.X, breachPos.Y), false, staticRadius, Color.Purple, 4, 64);
                
                if (breachInfo.ShouldShowExpansion)
                {
                    var timeSinceStart = (float)(DateTime.Now - breachInfo.ExpansionStartTime.Value).TotalSeconds;
                    var progress = Math.Min(timeSinceStart / TARGET_TIME, 1.0f);
                    var easedProgress = EaseOutQuad(progress);
                    var dynamicRadius = Settings.MaxCircleSize * easedProgress * mapScale;
                    
                    Graphics.DrawCircleOnLargeMap(new Vector2(breachPos.X, breachPos.Y), false, dynamicRadius, Color.White, 4, 64);
                    
                }
            }
            catch (Exception ex)
            {
                //Aboba
            }
        }
    }
}