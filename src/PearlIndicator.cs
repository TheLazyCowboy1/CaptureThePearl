using HUD;
using RainMeadow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CaptureThePearl;

/// <summary>
/// A weird conglomeration of PlayerSpecificOnlineHud and OnlinePlayerDisplay,
/// adapted for pearls specifically instead of just players.
/// </summary>
public class PearlIndicator : HudPart
{
    public RoomCamera camera;
    private Rect camrect;
    public Vector2 pos, lastPos;
    public bool found;
    public Vector2 pointDir;
    private WorldCoordinate lastWorldPos;
    private int lastCameraPos;
    private int lastAbstractRoom;

    public AbstractPhysicalObject apo;

    //actual indicator
    public FSprite arrowSprite;
    public FSprite pearlIcon;
    public Color color;
    public float alpha = 0.9f;

    public PearlIndicator(HUD.HUD hud, RoomCamera camera, AbstractPhysicalObject apo) : base(hud)
    {
        RainMeadow.RainMeadow.Debug("[CTP]: Adding PearlIndicator for " + apo);
        this.camera = camera;
        camrect = new Rect(Vector2.zero, this.camera.sSize).CloneWithExpansion(-30f);
        this.apo = apo;

        this.color = DataPearl.UniquePearlMainColor((apo as DataPearl.AbstractDataPearl).dataPearlType);

        //create sprites
        this.pos = new Vector2(-1000f, -1000f);
        this.lastPos = this.pos;

        this.pearlIcon = new FSprite("Symbol_Pearl", true);
        hud.fContainers[0].AddChild(this.pearlIcon);
        this.pearlIcon.alpha = 0.9f;
        this.pearlIcon.x = -1000f;
        this.pearlIcon.color = color;

        this.arrowSprite = new FSprite("Multiplayer_Arrow", true);
        hud.fContainers[0].AddChild(this.arrowSprite);
        this.arrowSprite.alpha = 0.9f;
        this.arrowSprite.x = -1000f;
        this.arrowSprite.color = color;
    }

    public override void Update()
    {
        base.Update();

        lastPos = pos;
        pos.x = -1000f; //move away if can't find pearl
        alpha = 0.9f;

        this.found = false;
        if (camera.room == null || !camera.room.shortCutsReady) return;

        Vector2 rawPos = new();
        // in this room
        if (apo.Room == camera.room.abstractRoom)
        {
            alpha = 0.5f;
            // in room or in shortcut
            if (apo.realizedObject is PhysicalObject obj)
            {
                if (obj.room == camera.room)
                {
                    found = true;
                    rawPos = obj.firstChunk.pos - camera.pos + new Vector2(0f, 20f); //put arrow above pearl if it's pointing down!
                    this.pointDir = Vector2.down;
                }
                else if (obj.grabbedBy.Count > 0)
                {
                    Vector2? shortcutpos = camera.game.shortcuts.OnScreenPositionOfInShortCutCreature(camera.room, obj.grabbedBy[0].grabber);
                    if (shortcutpos != null)
                    {
                        found = true;
                        rawPos = shortcutpos.Value - camera.pos;
                        this.pointDir = Vector2.down;
                    }
                }
            }

            if (found)
            {
                this.pos = camrect.GetClosestInteriorPoint(rawPos); // gives straight arrows
                if (pos != rawPos)
                {
                    pointDir = (rawPos - pos).normalized;
                }
            }
        }
        else // different room
        {
            // neighbor
            var connections = camera.room.abstractRoom.connections;
            for (int i = 0; i < connections.Length; i++)
            {
                if (apo.pos.room == connections[i])
                {
                    found = true;
                    var shortcutpos = camera.room.LocalCoordinateOfNode(i);
                    rawPos = camera.room.MiddleOfTile(shortcutpos) - camera.pos;
                    pointDir = camera.room.ShorcutEntranceHoleDirection(shortcutpos.Tile).ToVector2() * -1f;
                    break;
                }
            }
            if (found)
            {
                this.pos = camrect.GetClosestInteriorPoint(rawPos);
                Vector2 translation = pointDir * 10f; // Vector shift for shortcut viewability
                this.pos += translation;

                if (pos != rawPos)
                {
                    pointDir = (rawPos - pos).normalized * -1f; // Point away from the shortcut entrance
                }
            }
            else // elsewhere, use world pos
            {
                var world = camera.game.world;
                if (world.GetAbstractRoom(apo.pos.room) is AbstractRoom abstractRoom) // room in region
                {
                    found = true;
                    if (apo.pos != lastWorldPos || camera.currentCameraPosition != lastCameraPos || camera.room.abstractRoom.index != lastAbstractRoom) // cache these maths
                    {
                        var worldpos = (abstractRoom.mapPos / 3f + new Vector2(10f, 10f)) * 20f;
                        if (this.apo.realizedObject is PhysicalObject realObj) worldpos += realObj.firstChunk.pos - abstractRoom.size.ToVector2() * 20f / 2f;
                        else if (apo.pos.TileDefined) worldpos += apo.pos.Tile.ToVector2() * 20f - abstractRoom.size.ToVector2() * 20f / 2f;

                        var viewpos = (camera.room.abstractRoom.mapPos / 3f + new Vector2(10f, 10f)) * 20f + camera.pos + this.camera.sSize / 2f - camera.room.abstractRoom.size.ToVector2() * 20f / 2f;

                        pointDir = (worldpos - viewpos).normalized;
                        pos = camrect.GetClosestInteriorPointAlongLineFromCenter(this.camera.sSize / 2f + pointDir * 2048f); // gives angled arrows
                    }
                    else
                        pos = lastPos;
                }
            }
        }

        lastWorldPos = apo.pos;
        lastCameraPos = camera.currentCameraPosition;
        lastAbstractRoom = camera.room.abstractRoom.index;
    }

    public override void Draw(float timeStacker)
    {
        Vector2 vector = Vector2.Lerp(this.lastPos, this.pos, timeStacker) + new Vector2(0.01f, 0.01f);
        var pos = vector;

        this.arrowSprite.x = pos.x;
        this.arrowSprite.y = pos.y;
        this.arrowSprite.rotation = RWCustom.Custom.VecToDeg(pointDir * -1);
        this.arrowSprite.alpha = alpha;

        this.pearlIcon.x = pos.x;
        this.pearlIcon.y = pos.y + 16f;
        this.pearlIcon.alpha = alpha;

        base.Draw(timeStacker); //maybe this'll help, lol?
    }

    public override void ClearSprites()
    {
        base.ClearSprites();
        this.arrowSprite.RemoveFromContainer();
        this.pearlIcon.RemoveFromContainer();

        //if (CTPGameMode.IsCTPGameMode(out var gamemode))
        //gamemode.ClearIndicators(); //if it stopped being drawn, get rid of it!
        base.slatedForDeletion = true;
    }
}
