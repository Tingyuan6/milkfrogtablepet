import com.naifrog.pet.FartReaction;
public class FartReactionTests {
    static int checks;
    static void check(boolean ok){checks++;if(!ok)throw new AssertionError("fart check "+checks);}
    public static void main(String[] args){
        FartReaction f=new FartReaction();check(!f.active());check(f.visual()==null);
        for(String pose:new String[]{"idle","sit_1","sit_2","peek_left","peek_right","sway_00"}){
            check(!f.start(pose));check(!FartReaction.hit(pose,78,248));
        }
        check(FartReaction.hit("bottom",78,248));check(!FartReaction.hit("bottom",273,204));
        for(int r=0;r<5;r++){
            check(f.start("bottom"));
            for(int ms=0;ms<3000;ms+=10){check(f.active());check(f.visual().startsWith("effects/fart/"));f.advance(10);}
            check(!f.active()&&f.visual()==null);
        }
        f.start("bottom");f.advance(500);f.start("bottom");check(f.visual().endsWith("000.png"));f.stop();check(!f.active());
        System.out.println("PASS "+checks+" Java fart interaction checks");
    }
}
