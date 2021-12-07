using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace OpenTK_test
{
    struct Vertex
	{
		public Vector3 position;
		public Vector3 normal;
		public Vector2 uv;
        public Vector4 color;

		public Vertex(Vector3 position, Vector3 normal, Vector2 uv, Vector4 color)
		{
			this.position = position;
			this.normal = normal;
			this.uv = uv;
            this.color = color;
		}

		public static readonly int Size = Marshal.SizeOf(default(Vertex));
	}


    public class Game : GameWindow
    {
        #region Camera__Field

        bool isCameraRotating;      //カメラが回転状態かどうか
        Vector2 current, previous;  //現在の点、前の点
        Matrix4 rotate;             //回転行列
        float zoom;                 //拡大度
        float wheelPrevious;        //マウスホイールの前の状態

        #endregion

        int vertexShader;   //シェーダ
        int fragmentShader;
        int shaderProgram;

        Color4 materialAmbient;     //材質の環境光成分
        Color4 materialDiffuse;	//材質の拡散光成分
        Color4 materialSpecular;    //材質の鏡面光成分
        float materialShininess;	//材質の鏡面光の鋭さ

        //vbo-球
        Vertex[] vertices2;         //頂点
        int[] indices2;             //頂点の指標（InitSphere内で頂点を指定している）
        int vbo2;                   //VBOのバッファの識別番号を保持
        int ibo2;                   //IBOのバッファの識別番号を保持
        int vao2;					//VAOの識別番号を保持

        //トーラス
        Vertex[] vertices1;         //頂点
        int[] indices1;             //頂点の指標
        int vbo1;                   //VBOのバッファの識別番号を保持
        int ibo1;                   //IBOのバッファの識別番号を保持
        int vao1;					//VAOの識別番号を保持

        int ColorTexture;                //背景画像
        int size = 256;             //textureサイズ

        //試験用
        int DepthTexture;
        //fbo
        int fbo_screen;

        //画面作成
        public Game(int width, int height, string title) : base(width, height, GraphicsMode.Default, title)
        {
            #region Camera__Initialize

            isCameraRotating = false;
            current = OpenTK.Vector2.Zero;
            previous = OpenTK.Vector2.Zero;
            rotate = Matrix4.Identity;
            zoom = 1.0f;
            wheelPrevious = 0.0f;
            #endregion

            #region Camera__Event

            //マウスボタンが押されると発生するイベント
            this.MouseDown += (sender, e) =>
            {
                var mouse = OpenTK.Input.Mouse.GetState();
                //右ボタンが押された場合
                if (e.Button == MouseButton.Right)
                {
                    //ok
                    isCameraRotating = true;
                    current = new OpenTK.Vector2(mouse.X, mouse.Y);
                }
            };

            //マウスボタンが離されると発生するイベント
            this.MouseUp += (sender, e) =>
            {
                //右ボタンが押された場合
                if (e.Button == MouseButton.Right)
                {
                    isCameraRotating = false;
                    previous = OpenTK.Vector2.Zero;
                }
            };

            //マウスが動くと発生するイベント
            this.MouseMove += (sender, e) =>
            {
                ////カメラが回転状態の場合
                if (isCameraRotating)
                {
                    var mouse = OpenTK.Input.Mouse.GetState();
                    previous = current;
                    current = new OpenTK.Vector2(mouse.X, mouse.Y);
                    OpenTK.Vector2 delta = current - previous;
                    delta /= (float)Math.Sqrt(this.Width * this.Width + this.Height * this.Height);
                    float length = delta.Length;
                    if (length > 0.0)
                    {
                        float rad = length * MathHelper.Pi;
                        /*
                        float theta = (float)Math.Sin(rad) / length;
                        */
                        OpenTK.Vector3 after = new OpenTK.Vector3(
                            delta.Y,
                            delta.X,
                            0.0f);
                        Matrix4 diff = Matrix4.CreateFromAxisAngle(after, rad);
                        Matrix4.Mult(ref rotate, ref diff, out rotate);
                    }
                }
            };

            //マウスホイールが回転すると発生するイベント
            this.MouseWheel += (sender, e) =>
            {
                var mouse = OpenTK.Input.Mouse.GetState();
                float delta = (float)mouse.Wheel - (float)wheelPrevious;
                zoom *= (float)Math.Pow(1.06, delta);
                //拡大、縮小の制限
                if (zoom > 2.0f)
                    zoom = 2.0f;
                if (zoom < 0.5f)
                    zoom = 0.5f;
                wheelPrevious = mouse.Wheel;
            };

            #endregion

            materialAmbient = new Color4(0.2f, 0.2f, 0.2f, 1.0f);
            materialDiffuse = new Color4(0.7f, 0.7f, 0.7f, 1.0f);
            materialSpecular = new Color4(0.6f, 0.6f, 0.6f, 1.0f);
            materialShininess = 80.0f;

            //球
            vbo2 = 0;
            ibo2 = 0;
            vao2 = 0;
            //トーラス
            vbo1 = 0;
            ibo1 = 0;
            vao1 = 0;

            InitSphere(64, 32, 1.0f);//（縦の分割数⇒正面からは半分の面が見える,横の分割数,半径）
            InitTorus(32, 64, 0.5, 1.0);

            VSync = VSyncMode.On;
        }

        // 画面起動時に呼び出される
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            //ウィンドウの背景
            GL.ClearColor(0.3f, 0.5f, 0.8f, 0.0f);
            
            //Enable 使用可能にする（デプスバッファの使用）
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Normalize);
            GL.Enable(EnableCap.CullFace);
            GL.Enable(EnableCap.Multisample);
            GL.Enable(EnableCap.VertexProgramPointSize);//ポイントの設定
            GL.Enable(EnableCap.PointSprite);
            GL.Enable(EnableCap.Blend); // ブレンドの許可
            

            GL.Disable(EnableCap.Lighting);
            
            GL.Enable(EnableCap.Texture2D);

            //テクスチャ生成
            GL.GenTextures(1, out ColorTexture);
            GL.BindTexture(TextureTarget.Texture2D, ColorTexture);
            //fboの時
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, size, size, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.MirroredRepeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.MirroredRepeat);

           /*
            Bitmap file = new Bitmap("test1.png");
            //png画像の反転を直す
            file.RotateFlip(RotateFlipType.RotateNoneFlipY);
            //データ読み込み
            BitmapData data = file.LockBits(new Rectangle(0, 0, file.Width, file.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            //画像の時
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, data.Width, data.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
            */
            /*
            //裏面削除、反時計回りが表でカリング
            GL.Enable(EnableCap.CullFace);　//カリングの許可
            GL.CullFace(CullFaceMode.Back); //どちらの面を描画しないか
            GL.FrontFace(FrontFaceDirection.Ccw); //表を時計回り(Cw)か反時計回り(Ccw)か

            //ライティングON Light0を有効化
            //GL.Enable(EnableCap.Lighting);
            //GL.Enable(EnableCap.Light0); //Lightは最大8個まで

            #region vbo
            //VBOを1コ生成し、2の頂点データを送り込む
            GL.GenBuffers(1, out vbo2);
            //ArrayBufferとしてvbo2を指定（バインド）
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo2);
            int vertexArray2Size = vertices2.Length * Vertex.Size;
            //ArrayBufferにデータをセット
            GL.BufferData<Vertex>(BufferTarget.ArrayBuffer, new IntPtr(vertexArray2Size), vertices2, BufferUsageHint.StaticDraw);
            //バインド解除
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            */
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.NormalArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.EnableClientState(ArrayCap.ColorArray);
            //トーラス
            GL.GenBuffers(1, out vbo1);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo1);
            int vertexArray1Size = vertices1.Length * Vertex.Size;
            GL.BufferData<Vertex>(BufferTarget.ArrayBuffer, new IntPtr(vertices1.Length * Vertex.Size), vertices1, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            /*
            #endregion

            #region ibo
            GL.GenBuffers(1, out ibo2);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo2);
            int indexArray2Size = indices2.Length * sizeof(int);
            GL.BufferData(BufferTarget.ElementArrayBuffer, new IntPtr(indexArray2Size), indices2, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            */
            //トーラス
            GL.GenBuffers(1, out ibo1);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo1);
            int indexArray1Size = indices1.Length * sizeof(int);
            GL.BufferData(BufferTarget.ElementArrayBuffer, new IntPtr(indexArray1Size), indices1, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            /*
            #endregion

            #region vao
            //VAOを1コ作成
            GL.GenVertexArrays(1, out vao2);
            GL.BindVertexArray(vao2);
            //各Arrayを有効化
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.NormalArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo2);
            //頂点の位置、法線、テクスチャ情報の場所を指定
            GL.VertexPointer(3, VertexPointerType.Float, Vertex.Size, 0);
            GL.NormalPointer(NormalPointerType.Float, Vertex.Size, OpenTK.Vector3.SizeInBytes);
            GL.TexCoordPointer(2, TexCoordPointerType.Float, Vertex.Size, OpenTK.Vector3.SizeInBytes * 2);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            GL.BindVertexArray(0);
            */
            //VAOを1コ作成(トーラス)
            GL.GenVertexArrays(1, out vao1);
            //ここからVAO1
            GL.BindVertexArray(vao1);
            //各Arrayを有効化
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.NormalArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.EnableClientState(ArrayCap.ColorArray);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo1);

            //頂点の位置、法線、テクスチャ情報の場所を指定
            GL.VertexPointer(3, VertexPointerType.Float, Vertex.Size, 0);
            GL.NormalPointer(NormalPointerType.Float, Vertex.Size, Vector3.SizeInBytes);
            GL.TexCoordPointer(2, TexCoordPointerType.Float, Vertex.Size, Vector3.SizeInBytes * 2);
            //頂点の色情報の場所を指定(上の数を数える、スタート地点)
            GL.ColorPointer(4, ColorPointerType.Float, Vertex.Size, Vector3.SizeInBytes * 2 + Vector2.SizeInBytes);


            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            GL.BindVertexArray(0);
            /*
            //VAO1ここまで
            #endregion()
            //GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int)TextureEnvMode.Add);
            */

            
            #region Shader
            int status;
            //バーテックスシェーダを生成
            vertexShader = GL.CreateShader(ShaderType.VertexShader);
            using (var sr = new StreamReader("shader.vert"))
            {
                //バーテックスシェーダのコードを指定
                GL.ShaderSource(vertexShader, sr.ReadToEnd());
            }
            //バーテックスシェーダをコンパイル
            GL.CompileShader(vertexShader);
            GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out status);
            //コンパイル結果をチェック
            if (status == 0)
            {
                throw new ApplicationException(GL.GetShaderInfoLog(vertexShader));
            }

            //シェーダオブジェクト(フラグメント)を生成
            fragmentShader = GL.CreateShader(ShaderType.FragmentShader);

            using (var sr = new StreamReader("shader.frag"))
            {
                //フラグメントシェーダのコードを指定
                GL.ShaderSource(fragmentShader, sr.ReadToEnd());
            }
            //フラグメントシェーダをコンパイル
            GL.CompileShader(fragmentShader);
            GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out status);
            //コンパイル結果をチェック
            if (status == 0)
            {
                throw new ApplicationException(GL.GetShaderInfoLog(fragmentShader));
            }

            //シェーダプログラムの生成
            shaderProgram = GL.CreateProgram();
            //各シェーダオブジェクトをシェーダプログラムへ登録
            GL.AttachShader(shaderProgram, vertexShader);
            GL.AttachShader(shaderProgram, fragmentShader);
            //不要になったシェーダオブジェクトの削除
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
            //シェーダプログラムのリンク
            GL.LinkProgram(shaderProgram);
            //GL.GetProgram(shaderProgram, ProgramParameter.LinkStatus, out status);
            GL.GetProgram(shaderProgram, GetProgramParameterName.LinkStatus, out status);
            //シェーダプログラムのリンクのチェック
            if (status == 0)
            {
                throw new ApplicationException(GL.GetProgramInfoLog(shaderProgram));
            }
            //シェーダプログラムを使用
            GL.UseProgram(shaderProgram);
            #endregion
            
            #region fbo
            //一応Depthも試験的に作ってみる
            GL.GenTextures(1, out DepthTexture);
            GL.BindTexture(TextureTarget.Texture2D, DepthTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, (PixelInternalFormat)All.DepthComponent32, size, size, 0, OpenTK.Graphics.OpenGL.PixelFormat.DepthComponent, PixelType.UnsignedInt, IntPtr.Zero);
            // things go horribly wrong if DepthComponent's Bitcount does not match the main Framebuffer's Depth
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            GL.Ext.GenFramebuffers(1, out fbo_screen);
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo_screen);
            GL.Ext.FramebufferTexture2D(FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext, TextureTarget.Texture2D, ColorTexture, 0);
            GL.Ext.FramebufferTexture2D(FramebufferTarget.FramebufferExt, FramebufferAttachment.DepthAttachmentExt, TextureTarget.Texture2D, DepthTexture, 0);

            //エラーチェック
            FramebufferErrorCode fbostatus = GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt);
            if (fbostatus != FramebufferErrorCode.FramebufferComplete &&
                fbostatus != FramebufferErrorCode.FramebufferCompleteExt)
            {
                Console.WriteLine("Error creating framebuffer: {0}", status);
            }

            //FBO（https://github.com/mono/opentk/blob/main/Source/Examples/OpenGL/1.x/FramebufferObject.cs）
            GL.PushAttrib(AttribMask.ViewportBit);
            {
                GL.ClearColor(0.0f, 0.0f, 0.0f, 0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                GL.Viewport(0, 0, size, size);

                OpenTK.Matrix4 perspective = OpenTK.Matrix4.CreateOrthographic(size/2, size/2, 0f, 1.0f);
                GL.MatrixMode(MatrixMode.Projection);
                GL.LoadMatrix(ref perspective);

                Matrix4 lookat = Matrix4.LookAt(0f, 0f, 1.0f, 0f, 0f, 0f, 0f, 1f, 0f);
                GL.MatrixMode(MatrixMode.Modelview);
                GL.LoadMatrix(ref lookat);
                
                //FBOの加算を有効に
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
                GL.BlendEquation(BlendEquationMode.FuncAdd);
                
                //http://penguinitis.g1.xrea.com/computer/programming/OpenGL/23-blend.html
                GL.Disable(EnableCap.DepthTest);

                MakeMap(1.0f, 256f, 0.0f);

                GL.Disable(EnableCap.Blend);
                GL.Enable(EnableCap.DepthTest);
                
            }
            GL.PopAttrib();
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, 0); // disable rendering into the FBO

            //初期画面背景
            GL.ClearColor(Color4.DarkBlue);
            #endregion fbo
            
        }

        //ウィンドウの終了時に実行される。
        protected override void OnUnload(EventArgs e)
        {
            base.OnUnload(e);

            GL.DeleteBuffers(1, ref vbo1);          //バッファを1コ削除
            GL.DeleteBuffers(1, ref ibo1);          //バッファを1コ削除
            GL.DeleteVertexArrays(1, ref vao1);		//VAOを1コ削除

            GL.DeleteBuffers(1, ref vbo2);          //バッファを1コ削除
            GL.DeleteBuffers(1, ref ibo2);          //バッファを1コ削除
            GL.DeleteVertexArrays(1, ref vao2);     //VAOを1コ削除

            GL.DisableClientState(ArrayCap.VertexArray);    //VertexArrayを無効化
            GL.DisableClientState(ArrayCap.NormalArray);    //NormalArrayを無効化
            GL.DisableClientState(ArrayCap.ColorArray);		//ColorArrayを無効化
            GL.DisableClientState(ArrayCap.TextureCoordArray);
            GL.DeleteTexture(ColorTexture);   //使用したテクスチャの削除

            GL.DeleteProgram(shaderProgram);//シェーダの削除

            GL.DeleteFramebuffers(1, ref fbo_screen);
        }

        //画面更新時に呼ばれる
        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            KeyboardState input = Keyboard.GetState();
            if (input.IsKeyDown(Key.Escape))
            {
                Close();
            }

            #region Camera__Keyboard

            //F1キーで回転をリセット
            if (input.IsKeyDown(Key.F1))
            {
                rotate = Matrix4.Identity;
            }

            //F2キーでY軸90度回転
            if (input.IsKeyDown(Key.F2))
            {
                rotate = Matrix4.CreateRotationY(MathHelper.PiOver2);
            }

            //F3キーでY軸180度回転
            if (input.IsKeyDown(Key.F3))
            {
                rotate = Matrix4.CreateRotationY(MathHelper.Pi);
            }

            //F4キーでY軸270度回転
            if (input.IsKeyDown(Key.F4))
            {
                rotate = Matrix4.CreateRotationY(MathHelper.ThreePiOver2);
            }

            //F5キーで拡大をリセット
            if (input.IsKeyDown(Key.F5))
            {
                zoom = 1.0f;
            }

            #endregion
        }

        //画面が描画されるときに呼び出し
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            #region TransFormationMatrix

            Matrix4 modelView = Matrix4.LookAt(Vector3.UnitZ * 10 / zoom, Vector3.Zero, Vector3.UnitY);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref modelView);
            GL.MultMatrix(ref rotate);

            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4 / zoom, (float)this.Width / (float)this.Height, 1.0f, 64.0f);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref projection);

            #endregion

            //材質のパラメータ設定（表裏、材質の要素、その情報）            
            GL.Material(MaterialFace.Front, MaterialParameter.Ambient, materialAmbient);
            GL.Material(MaterialFace.Front, MaterialParameter.Diffuse, materialDiffuse);
            GL.Material(MaterialFace.Front, MaterialParameter.Specular, materialSpecular);
            GL.Material(MaterialFace.Front, MaterialParameter.Shininess, materialShininess);

            GL.BindTexture(TextureTarget.Texture2D, ColorTexture);

            GL.UseProgram(0);

            //1を描画
            GL.BindVertexArray(vao1);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo1);
            GL.DrawElements(BeginMode.Quads, indices1.Length, DrawElementsType.UnsignedInt, 0);

            GL.PopMatrix();

            GL.BindTexture(TextureTarget.Texture2D, 0);

            /*
            GL.UseProgram(shaderProgram);
            GL.PointSize(10000f);
            GL.Begin(BeginMode.Points);
            GL.Color4(1.0f, 0.0f, 1.0f, 1.0f);
            GL.Vertex3(1.3, 1, 3);
            GL.Vertex3(1, 1, 3);
            GL.End();
            GL.Enable(EnableCap.DepthTest);
            GL.UseProgram(0);
            */

            GL.BindTexture(TextureTarget.Texture2D, ColorTexture);
            //メインのポリゴン表示  
            GL.Color4(Color4.White);
            GL.Begin(BeginMode.Quads);
            GL.TexCoord2(1.0, 1.0);
            GL.Vertex3(4, 1, 0);
            GL.TexCoord2(0.0, 1.0);
            GL.Vertex3(2, 1, 0);
            GL.TexCoord2(0.0, 0.0);
            GL.Vertex3(2, -1, 0);
            GL.TexCoord2(1.0, 0.0);
            GL.Vertex3(4, -1, 0);
            GL.End();
            
            GL.BindTexture(TextureTarget.Texture2D, 0);

            SwapBuffers();
        }

        //ウィンドウのサイズが変わるたびに読み込み
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            //ビューポートの設定
            GL.Viewport(ClientRectangle);
        }

        //球で初期化
        void InitSphere(int slice, int stack, float radius)
        {
            /*
            for (int i = 0; i < 32 * 32; i++)
            {
                //値が参照出来ていない  1011 これでOK
                Debug.WriteLine(MainWindow.irradiancemap[i, 0] + " , " + MainWindow.irradiancemap[i, 1]);
            }
            */

            LinkedList<Vertex> vertexList = new LinkedList<Vertex>();
            LinkedList<int> indexList = new LinkedList<int>();

            for (int i = 0; i <= stack; i++)
            {
                double p = Math.PI / stack * i;
                double pHeight = Math.Cos(p);
                double pWidth = Math.Sin(p);

                for (int j = 0; j <= slice; j++)
                {
                    double rotor = 2 * Math.PI / slice * j;
                    double x = Math.Cos(rotor);
                    double y = Math.Sin(rotor);

                    OpenTK.Vector3 position = new OpenTK.Vector3((float)(radius * x * pWidth), (float)(radius * pHeight), (float)(radius * y * pWidth));
                    OpenTK.Vector3 normal = new OpenTK.Vector3((float)(x * pWidth), (float)pHeight, (float)(y * pWidth));
                    OpenTK.Vector2 uv = new OpenTK.Vector2((float)((1 + x * pWidth) / 2), (float)((1 + pHeight) / 2));
                    OpenTK.Vector4 color = new OpenTK.Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                    vertexList.AddLast(new Vertex(position, normal, uv, color));
                }
            }

            for (int i = 0; i <= stack; i++)
            {
                for (int j = 0; j <= slice; j++)
                {
                    int d = i * (slice + 1) + j;
                    indexList.AddLast(d);
                    indexList.AddLast(d + 1);
                    indexList.AddLast(d + slice + 2);
                    indexList.AddLast(d + slice + 1);
                }
            }
            vertices2 = vertexList.ToArray();
            indices2 = indexList.ToArray();
        }


        //トーラスの初期化(色がおかしくなる)
        void InitTorus(int row, int column, double smallRadius, double largeRadius)
        {
            LinkedList<Vertex> vertexList = new LinkedList<Vertex>();
            LinkedList<int> indexList = new LinkedList<int>();
            for (int i = 0; i <= row; i++)
            {
                double sr = (2.0 * Math.PI / row) * i;
                double cossr = Math.Cos(sr);
                double sinsr = Math.Sin(sr);
                double sx = cossr * smallRadius;
                double sy = sinsr * smallRadius;
                for (int j = 0; j <= column; j++)
                {
                    double lr = (2.0 * Math.PI / column) * j;
                    double coslr = Math.Cos(lr);
                    double sinlr = Math.Sin(lr);
                    double px = coslr * (sx + largeRadius);
                    double py = sy;
                    double pz = sinlr * (sx + largeRadius);
                    double nx = cossr * coslr;
                    double ny = sinsr;
                    double nz = cossr * sinlr;
                    OpenTK.Vector3 position = new OpenTK.Vector3((float)px, (float)py, (float)pz);
                    OpenTK.Vector3 normal = new OpenTK.Vector3((float)nx, (float)ny, (float)nz);
                    OpenTK.Vector2 uv = new OpenTK.Vector2((float)(nx / 2 + 0.5f), (float)(ny / 2 + 0.5f));
                    OpenTK.Vector4 color = new OpenTK.Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                    vertexList.AddLast(new Vertex(position, normal, uv, color));
                }
            }
            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < column; j++)
                {
                    int d = i * (column + 1) + j;
                    indexList.AddLast(d);
                    indexList.AddLast(d + column + 1);
                    indexList.AddLast(d + column + 2);
                    indexList.AddLast(d + 1);
                }
            }
            vertices1 = vertexList.ToArray();
            indices1 = indexList.ToArray();
        }

        //放射照度マップの描画(w：描画範囲の縦横幅)
        void MakeMap(float wh, float r, float depth)
        {
            GL.PointSize(r);
            GL.Begin(BeginMode.Points);
            GL.Color4(0.0f, 1.0f, 0.1f, 1.0f);
            GL.Vertex3(0.0f, 0.0f, depth);
            GL.End();
        }
    }

}
