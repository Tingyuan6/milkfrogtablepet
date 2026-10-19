import com.naifrog.pet.FaceMotion;

public final class FaceMotionTests {
    private static int checks;
    private static void check(boolean ok,String why){checks++;if(!ok)throw new AssertionError(why);}
    public static void main(String[] args){
        double[][] samples={{0,0},{1,.6},{0,0}};
        FaceMotion f=new FaceMotion(samples,1000);
        f.advance(2999,true,0,false);check(f.blink==0,"wait three seconds");
        f.advance(41,true,0,false);check(f.blink>.4&&f.blink<.6,"closing in-between");
        f.advance(45,true,0,false);check(f.blink==1,"closed");
        f.advance(95,true,0,false);check(f.blink>0&&f.blink<1,"opening in-between");
        f.advance(60,true,0,false);check(f.blink==0,"reopen");
        f.advance(2845,true,0,false);check(f.blink==1,"period three seconds");
        f.touch();check(f.blink==0,"input resets");
        f.advance(10000,false,0,false);check(f.blink==0,"transition defers blink");
        f.advance(100,true,1000,true);check(f.mouth>.9,"media position controls lips");
        f.advance(500,true,2000,false);check(f.mouth==0,"completion closes lips");
        f.advance(100,true,1000,false);check(f.mouth==0,"stopped audio cannot open mouth");
        for(int cycle=0;cycle<100;cycle++){f.touch();f.advance(3000+85,true,0,false);check(f.blink==1,"repeat blink "+cycle);}
        System.out.println("PASS "+checks+" Java facial timeline checks");
    }
}
