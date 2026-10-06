
public interface IAnimation 
{

    public float MoveToA();
    public float MoveToB();
    public float MoveToAThenB(float delay);
    public float MoveToBThenA(float delay);
    public float TogglePosition();

    //Para los eventos de inspector 
    public void vMoveToA() => MoveToA();
    public void vMoveToB() => MoveToB();
    public void vMoveToAThenB(float delay) => MoveToAThenB(delay);
    public void vMoveToBThenA(float delay) => MoveToBThenA(delay);
    public void vTogglePosition() => TogglePosition();
}
