using Wargon.ezs;

namespace App {
    public abstract class UpdateSystem {
        protected World world;
        protected Entities entities => new();

        public virtual void OnInit() { }

        public abstract void Update();

        public virtual void UpdateN() { }
    }

    public interface IUpdate {
        void OnUpdate();
    }

    public class What : IUpdate {
        public void OnUpdate() { }

        public void OnUpdateGenerated() { }
    }
}