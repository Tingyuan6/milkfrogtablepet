import com.naifrog.pet.PetPlacement;

public final class PetPlacementTests {
    private static int checks;
    private static void check(boolean condition,String message){checks++;if(!condition)throw new AssertionError(message);}
    public static void main(String[] args){
        boolean rejects=false;try{PetPlacement.center(0,0,1,0,0);}catch(IllegalArgumentException expected){rejects=true;}
        check(rejects,"unmeasured layout cannot place character offscreen");
        for(float density:new float[]{1,1.5f,2,2.75f,3.5f,4})for(int widthDp:new int[]{320,360,412,800})for(int heightDp:new int[]{600,800,1100}){
            int width=Math.round(widthDp*density),height=Math.round(heightDp*density);float scale=density*.85f;
            float[] p=PetPlacement.center(width,height,scale,Math.round(350*density),Math.round((heightDp-70)*density));
            check(p[0]+86*scale>=-.1&&p[0]+234*scale<=width+.1,"body horizontally visible");
            check(p[1]+30*scale>=-.1&&p[1]+290*scale<=height+.1,"body vertically visible");
            check(!Float.isNaN(p[0])&&!Float.isNaN(p[1]),"finite initial coordinates");
        }
        float[] narrow=PetPlacement.center(500,380,2,300,330);
        check(narrow[1]+290*2>0&&narrow[1]+30*2<380,"short split-screen still intersects character");
        System.out.println("PASS "+checks+" Android placement checks");
    }
}
