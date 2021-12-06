using System;

namespace OpenTK_test
{
    class Program
    {
        static void Main(string[] args)
        {
            // This line creates a new instance, and wraps the instance in a using statement so it's automatically disposed once we've exited the block.
            //この行は、新しいインスタンスを作成し、インスタンスをusingステートメントでラップして、ブロックを終了すると自動的に破棄されるようにします。
            using (Game game = new Game(800, 600, "LearnOpenTK"))
            {
                //Run takes a double, which is how many frames per second it should strive to reach.
                //You can leave that out and it'll just update as fast as the hardware will allow it.
                game.Run(10.0);
            }
        }


    }
}
