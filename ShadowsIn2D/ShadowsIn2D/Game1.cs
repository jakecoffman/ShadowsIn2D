using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using ShadowsIn2D.Visibility;
using RoyT.XNA;

namespace ShadowsIn2D
{   
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        List<Visual> blocks;         

        List<PointLight> lights;     

        RenderTarget2D colorMap;
        RenderTarget2D lightMap;
        RenderTarget2D blurMap;
        
        Effect lightEffect;
        Effect combineEffect;
        Effect blurEffect;

        Quad quad;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferHeight = 720;
            graphics.PreferredBackBufferWidth = 1280;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }
     
        protected override void Initialize()
        {
            base.Initialize();
        }
     
        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            int windowWidth = GraphicsDevice.Viewport.Width;
            int windowHeight = GraphicsDevice.Viewport.Height;

            // Set up all render targets, the blur map doesn't need a depth buffer
            colorMap = new RenderTarget2D(GraphicsDevice, windowWidth, windowHeight, false, SurfaceFormat.Color, DepthFormat.Depth16, 16, RenderTargetUsage.DiscardContents);
            lightMap = new RenderTarget2D(GraphicsDevice, windowWidth, windowHeight, false, SurfaceFormat.Color, DepthFormat.Depth16, 16, RenderTargetUsage.DiscardContents);
            blurMap = new RenderTarget2D(GraphicsDevice, windowWidth, windowHeight, false, SurfaceFormat.Color, DepthFormat.None, 16, RenderTargetUsage.DiscardContents);
            
            combineEffect = Content.Load<Effect>("Combine");
            lightEffect = Content.Load<Effect>("Light");
            blurEffect = Content.Load<Effect>("Blur");

            quad = new Quad();

            Texture2D blockTexture = Content.Load<Texture2D>("Block");
            Texture2D blockGlow = Content.Load<Texture2D>("BlockGlow");

            blocks = new List<Visual>();

            // Add blocks
            blocks.Add(new Visual(blockTexture, new Vector2(250, 150), 0, blockGlow));
            blocks.Add(new Visual(blockTexture, new Vector2(windowWidth - 250, 150), 0, blockGlow));
            blocks.Add(new Visual(blockTexture, new Vector2(250, windowHeight - 150), 0.0f, blockGlow));
            blocks.Add(new Visual(blockTexture, new Vector2(windowWidth - 250, windowHeight - 150), 0.0f, blockGlow));

            for (int i = 0; i < 10; i++)
            {
                Vector2 position = new Vector2(150 + (10 * (i + 1)) * i, windowHeight / 2.0f);
                Pose2D p = new Pose2D(position, (MathHelper.PiOver2 / 10) * i, (i + 1) * 0.15f);

                blocks.Add(new Visual(blockTexture, p, blockGlow));
            }          

            // Add lights
            lights = new List<PointLight>();

            lights.Add(new PointLight(lightEffect, new Vector2(300, 300), 500, Color.White, 1.0f));

            //lights.Add(new PointLight(lightEffect, new Vector2(100, 300), 500, Color.Red, 1.0f));
            //lights.Add(new PointLight(lightEffect, new Vector2(800, 300), 500, Color.Green, 1.0f));
            //lights.Add(new PointLight(lightEffect, new Vector2(100, 600), 500, Color.Blue, 1.0f));
            //lights.Add(new PointLight(lightEffect, new Vector2(800, 600), 500, Color.Yellow, 1.0f));                                   
        }
   
        protected override void UnloadContent()
        {

        }

        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();
      
            // Animate the first block
            blocks[0].Pose.Rotation = (float)gameTime.TotalGameTime.TotalSeconds;
          
            base.Update(gameTime);
        }
    
        protected override void Draw(GameTime gameTime)
        {            
            // Draw the colors
            DrawColorMap();
            
            // Draw the lights
            DrawLightMap(0.0f);                     
            
            // Blurr the shadows
            BlurRenderTarget(lightMap, 2.5f);

            // Combine
            CombineAndDraw();                                  
            
            base.Draw(gameTime);
        }

        /// <summary>
        /// Combines what is in the color map with the lighting in the light map
        /// </summary>
        private void CombineAndDraw()
        {
            GraphicsDevice.SetRenderTarget(null);

            GraphicsDevice.Clear(Color.Black);

            GraphicsDevice.BlendState = BlendState.Opaque;
            // Samplers states are set by the shader itself            
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;

            combineEffect.Parameters["colorMap"].SetValue(colorMap);
            combineEffect.Parameters["lightMap"].SetValue(lightMap);

            combineEffect.Techniques[0].Passes[0].Apply();
            quad.Render(GraphicsDevice, Vector2.One * -1.0f, Vector2.One);
        }

        /// <summary>
        /// Blurs the target render target
        /// </summary>        
        private void BlurRenderTarget(RenderTarget2D target, float strength)
        {
            Vector2 renderTargetSize = new Vector2
              (
                  target.Width,
                  target.Height
              );

            blurEffect.Parameters["renderTargetSize"].SetValue(renderTargetSize);            
            blurEffect.Parameters["blur"].SetValue(strength);

            // Pass one
            GraphicsDevice.SetRenderTarget(blurMap);
            GraphicsDevice.Clear(Color.Black);

            blurEffect.Parameters["InputTexture"].SetValue(target);
           
            blurEffect.CurrentTechnique = blurEffect.Techniques["BlurHorizontally"];
            blurEffect.CurrentTechnique.Passes[0].Apply();
            quad.Render(GraphicsDevice, Vector2.One * -1, Vector2.One);

            // Pass two
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(Color.Black);
            
            blurEffect.Parameters["InputTexture"].SetValue(blurMap);

            blurEffect.CurrentTechnique = blurEffect.Techniques["BlurVertically"];
            blurEffect.CurrentTechnique.Passes[0].Apply();
            quad.Render(GraphicsDevice, Vector2.One * -1, Vector2.One);
        }

        /// <summary>
        /// Draws everything that emits light to a seperate render target
        /// </summary>
        private void DrawLightMap(float ambientLightStrength)
        {
            GraphicsDevice.SetRenderTarget(lightMap);
            GraphicsDevice.Clear(Color.White * ambientLightStrength);

            // Draw normal object that emit light
            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise);

            foreach(Visual v in blocks)
            {
                if(v.Glow != null)
                {
                    Vector2 origin = new Vector2(v.Glow.Width / 2.0f, v.Glow.Height / 2.0f);
                    spriteBatch.Draw(v.Glow, v.Pose.Position, null, Color.White, v.Pose.Rotation, origin, v.Pose.Scale, SpriteEffects.None, 0);
                }
            }

            spriteBatch.End();

            GraphicsDevice.BlendState = BlendState.Additive;
            // Samplers states are set by the shader itself            
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            if (Mouse.GetState().RightButton == ButtonState.Pressed)
            {
                lights[0].Position = new Vector2(Mouse.GetState().X, Mouse.GetState().Y);
            }
            
            foreach (PointLight l in lights)
            {                
                l.Render(GraphicsDevice, blocks);
            }
        }

        /// <summary>
        /// Draws all normal objects
        /// </summary>
        private void DrawColorMap()
        {
            GraphicsDevice.SetRenderTarget(colorMap);
            GraphicsDevice.Clear(Color.CornflowerBlue);

            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise, null);

            foreach (Visual v in blocks)
            {
                Vector2 origin = new Vector2(v.Texture.Width / 2.0f, v.Texture.Height / 2.0f);

                spriteBatch.Draw(v.Texture, v.Pose.Position, null, Color.White, v.Pose.Rotation, origin, v.Pose.Scale, SpriteEffects.None, 0);
            }

            spriteBatch.End();
        }       
    }
}
