using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    [Header("Configuración de la grid")]
    public int width = 50;
    public int height = 30;

    [Tooltip("Segundos entre cada día o tick de simulación")]
    public float updateTime = 0.1f;

    [Header("Condiciones iniciales")]
    [Range(0f, 1f)]
    public float initialSandChance = 0.15f;

    [Tooltip("Cantidad de filas superiores donde aparecerá arena al reiniciar")]
    public int initialSandRows = 5;

    private bool[,] grid;

    private float timer;
    private bool isPaused;
    private int generation;

    private Texture2D texture;
    private Color32[] pixels;

    private static readonly Color32 SandColor =
        new Color32(224, 185, 95, 255);

    private static readonly Color32 EmptyColor =
        new Color32(35, 40, 48, 255);

    void Start()
    {
        // La grid se crea una sola vez.
        grid = new bool[width, height];

        // Conexión con los eventos del InputManager.
        InputManager.Instance.OnPause += TogglePause;
        InputManager.Instance.OnRestart += RestartSimulation;
        InputManager.Instance.OnClear += ClearSimulation;
        InputManager.Instance.OnToggleCell += ToggleCellInput;

        BuildTexture();
        RandomizeGrid();
    }

    void Update()
    {
        if (isPaused)
            return;

        timer += Time.deltaTime;

        if (timer >= updateTime)
        {
            Step();
            UpdateVisuals();

            timer = 0f;
        }
    }

    // Ejecuta una generación o día de simulación.
    void Step()
    {
        /*
         * Se recorre desde abajo hacia arriba.
         *
         * Cuando una partícula baja a una fila inferior,
         * esa fila ya fue procesada y la partícula no puede
         * moverse dos veces durante el mismo tick.
         */
        for (int y = 1; y < height; y++)
        {
            // Alternar el recorrido reduce la inclinación hacia un lado.
            bool leftToRight = Random.value < 0.5f;

            if (leftToRight)
            {
                for (int x = 0; x < width; x++)
                {
                    MoveSandParticle(x, y);
                }
            }
            else
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    MoveSandParticle(x, y);
                }
            }
        }

        generation++;

        Debug.Log(
            "Día " + generation +
            " | Partículas de arena: " + CountSandParticles()
        );
    }

    void MoveSandParticle(int x, int y)
    {
        // Si la celda está vacía, no hay nada que mover.
        if (!grid[x, y])
            return;

        // Regla 1: caída vertical.
        if (IsEmpty(x, y - 1))
        {
            MoveParticle(x, y, x, y - 1);
            return;
        }

        // Comprobación de movimientos diagonales.
        bool canMoveLeft = IsEmpty(x - 1, y - 1);
        bool canMoveRight = IsEmpty(x + 1, y - 1);

        // Regla 3.1: ambas diagonales están libres.
        if (canMoveLeft && canMoveRight)
        {
            if (Random.value < 0.5f)
                MoveParticle(x, y, x - 1, y - 1);
            else
                MoveParticle(x, y, x + 1, y - 1);

            return;
        }

        // Regla 3.2: solamente abajo-izquierda está libre.
        if (canMoveLeft)
        {
            MoveParticle(x, y, x - 1, y - 1);
            return;
        }

        // Regla 3.3: solamente abajo-derecha está libre.
        if (canMoveRight)
        {
            MoveParticle(x, y, x + 1, y - 1);
            return;
        }

        /*
         * Regla 4: si abajo y las dos diagonales están
         * ocupadas, no se realiza ningún movimiento.
         */
    }

    // Comprueba que una posición exista y esté vacía.
    bool IsEmpty(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        return !grid[x, y];
    }

    // Traslada una partícula de una celda a otra.
    void MoveParticle(int fromX, int fromY, int toX, int toY)
    {
        grid[fromX, fromY] = false;
        grid[toX, toY] = true;
    }

    int CountSandParticles()
    {
        int amount = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y])
                    amount++;
            }
        }

        return amount;
    }

    void TogglePause()
    {
        isPaused = !isPaused;

        Debug.Log(
            isPaused
                ? "Simulación pausada"
                : "Simulación reanudada"
        );
    }

    void RestartSimulation()
    {
        Debug.Log("Reiniciando la simulación de arena...");

        generation = 0;
        timer = 0f;

        RandomizeGrid();
    }

    void ClearSimulation()
    {
        Debug.Log("Limpiando la simulación...");

        generation = 0;
        timer = 0f;

        ClearGrid();
    }

    void ToggleCellInput()
    {
        if (Mouse.current != null)
        {
            HandleMouseClick();
            return;
        }

        if (Camera.main != null)
        {
            ToggleCellAtWorld(Camera.main.transform.position);
        }
    }

    void BuildTexture()
    {
        texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false
        );

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        pixels = new Color32[width * height];

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, width, height),
            Vector2.zero,
            1f
        );

        SpriteRenderer spriteRenderer =
            GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer =
                gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = sprite;
    }

    public void ClearGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = false;
            }
        }

        UpdateVisuals();
    }

    void RandomizeGrid()
    {
        ClearGrid();

        /*
         * La arena aparece únicamente en las filas superiores.
         * Estas constituyen la condición inicial del sistema.
         */
        int rows = Mathf.Clamp(initialSandRows, 1, height);
        int firstRow = height - rows;

        for (int x = 0; x < width; x++)
        {
            for (int y = firstRow; y < height; y++)
            {
                grid[x, y] =
                    Random.value < initialSandChance;
            }
        }

        UpdateVisuals();

        Debug.Log(
            "Condición inicial creada | Partículas: " +
            CountSandParticles()
        );
    }

    void HandleMouseClick()
    {
        if (Camera.main == null)
            return;

        Vector3 worldPosition =
            Camera.main.ScreenToWorldPoint(
                Mouse.current.position.ReadValue()
            );

        ToggleCellAtWorld(worldPosition);
    }

    void ToggleCellAtWorld(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x);
        int y = Mathf.FloorToInt(worldPosition.y);

        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        grid[x, y] = !grid[x, y];

        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        for (int y = 0; y < height; y++)
        {
            int row = y * width;

            for (int x = 0; x < width; x++)
            {
                pixels[row + x] =
                    grid[x, y] ? SandColor : EmptyColor;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
    }

    void OnDestroy()
    {
        if (InputManager.Instance == null)
            return;

        InputManager.Instance.OnPause -= TogglePause;
        InputManager.Instance.OnRestart -= RestartSimulation;
        InputManager.Instance.OnClear -= ClearSimulation;
        InputManager.Instance.OnToggleCell -= ToggleCellInput;
    }
}
