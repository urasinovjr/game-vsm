using UnityEngine;

namespace GameVSM
{
    // Carried work props only; they do not replace any equipment in the train.
    public static class ActionProps
    {
        static Mesh radio, clipboard;
        static Material[] radioMaterials, clipboardMaterials;

        public static Transform CreateRadio(Transform parent)
        {
            if (radio == null)
            {
                var b = new PropMeshBuilder();
                b.RoundedBox(Vector3.zero, new Vector3(.055f, .12f, .03f), .006f, 0);
                // Positive Z is the front: speaker, small display and a tactile side key.
                b.RoundedBox(new Vector3(0, .018f, .0145f), new Vector3(.038f, .024f, .002f), .001f, 1, 1);
                for (int i = 0; i < 5; i++)
                    b.Tube(new Vector3(-.017f, -.009f - .006f * i, .015f), new Vector3(.017f, -.009f - .006f * i, .015f), .0013f, 1, 6);
                b.RoundedBox(new Vector3(.027f, .011f, 0), new Vector3(.005f, .026f, .015f), .002f, 1, 1);
                b.Tube(new Vector3(-.018f, .054f, 0), new Vector3(-.018f, .115f, 0), .0035f, 0, 10);
                b.Tube(new Vector3(.015f, .055f, 0), new Vector3(.015f, .066f, 0), .006f, 1, 10);
                b.RoundedBox(new Vector3(0, .018f, .0158f), new Vector3(.029f, .013f, .001f), .0004f, 2, 1);
                radio = b.Build("Action_Radio");
                radioMaterials = new[] {
                    PropMeshBuilder.Material("Action_RadioCase", new Color(.045f, .055f, .065f), .3f),
                    PropMeshBuilder.Material("Action_RadioRubber", new Color(.016f, .020f, .023f), .15f),
                    PropMeshBuilder.Material("Action_RadioDisplay", new Color(.12f, .28f, .27f), .5f)
                };
            }
            return Create(parent, "Рабочая рация", radio, radioMaterials);
        }

        public static Transform CreateClipboard(Transform parent)
        {
            if (clipboard == null)
            {
                var b = new PropMeshBuilder();
                // An A5 sheet fits inside the slightly larger 16 x 23 cm board.
                b.RoundedBox(Vector3.zero, new Vector3(.16f, .23f, .005f), .002f, 0);
                b.RoundedBox(new Vector3(0, -.002f, .0031f), new Vector3(.148f, .210f, .0012f), .0004f, 1, 1);
                b.RoundedBox(new Vector3(0, .099f, .007f), new Vector3(.058f, .018f, .006f), .002f, 2, 1);
                b.Tube(new Vector3(-.016f, .106f, .009f), new Vector3(.016f, .106f, .009f), .002f, 2, 8);
                // These are restrained form rules, not fake readable instructions.
                for (int i = 0; i < 9; i++)
                    b.Box(new Vector3(i == 0 ? -.012f : 0, .072f - .018f * i, .0038f),
                        new Vector3(i == 0 ? .10f : .12f, i == 0 ? .0016f : .0006f, .0002f), 0);
                clipboard = b.Build("Action_Clipboard");
                clipboardMaterials = new[] {
                    PropMeshBuilder.Material("Action_ClipboardBoard", new Color(.13f, .18f, .21f), .28f),
                    PropMeshBuilder.Material("Action_ClipboardPaper", new Color(.82f, .81f, .76f), .13f),
                    PropMeshBuilder.Material("Action_ClipboardClip", new Color(.55f, .57f, .59f), .55f, .8f)
                };
            }
            return Create(parent, "Планшет с листом", clipboard, clipboardMaterials);
        }

        static Transform Create(Transform parent, string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = materials; return go.transform;
        }
    }
}
