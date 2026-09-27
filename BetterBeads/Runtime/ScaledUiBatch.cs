using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BetterBeads.Runtime;

/// <summary>Temporarily scales the existing UI batch and restores its complete state.</summary>
internal sealed class ScaledUiBatch:IDisposable
{
    private static readonly Dictionary<string,FieldInfo> fields=new[]{"_sortMode","_blendState","_samplerState","_depthStencilState","_rasterizerState","_effect","_spriteEffect"}
        .ToDictionary(name=>name,name=>AccessTools.Field(typeof(SpriteBatch),name)??throw new MissingFieldException(typeof(SpriteBatch).FullName,name));
    private readonly SpriteBatch batch;
    private readonly SpriteSortMode sort;
    private readonly BlendState blend;
    private readonly SamplerState sampler;
    private readonly DepthStencilState depth;
    private readonly RasterizerState raster;
    private readonly Effect? effect;
    private readonly Matrix? matrix;
    private static T Read<T>(SpriteBatch b,string name)=>(T)fields[name].GetValue(b)!;
    public ScaledUiBatch(SpriteBatch batch,float scale)
    {
        this.batch=batch;
        sort=Read<SpriteSortMode>(batch,"_sortMode");blend=Read<BlendState>(batch,"_blendState");sampler=Read<SamplerState>(batch,"_samplerState");
        depth=Read<DepthStencilState>(batch,"_depthStencilState");raster=Read<RasterizerState>(batch,"_rasterizerState");effect=Read<Effect?>(batch,"_effect");
        matrix=Read<SpriteEffect>(batch,"_spriteEffect").TransformMatrix;
        batch.End();
        batch.Begin(sort,blend,SamplerState.PointClamp,depth,raster,effect,Matrix.CreateScale(scale)*(matrix??Matrix.Identity));
    }
    public void Dispose()
    {
        batch.End();batch.Begin(sort,blend,sampler,depth,raster,effect,matrix);
    }
}
