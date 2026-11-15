using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Jefe del Nivel 1: "Centinela de Acceso".
// Máquina de estados: Inactivo → Idle → Caminar → Ataque → (Aturdido) → Alarma → Muerte.
// Ataques (cada uno con aviso visual antes):
//   - Ráfaga: levanta la ametralladora y dispara balas lentas horizontales, altas (agacharse) o bajas (saltar).
//   - Embestida: ojo/luz roja ~0.8 s y corre recto hacia el jugador. Si choca con la pared de la arena
//     queda aturdido (daño x2, panel trasero expuesto); si no, se detiene y se recupera.
//   - Llamado de drones (solo fase 2): alarma y aparecen hasta 2 drones.
// Fase 2 (50% de vida): alarma, temblor, tinte rojo pulsante, +35% velocidad, -30% cooldowns.
// Al 20%: se rompe el blindaje (partículas) y recibe +25% de daño.
// La arena (JefeArena) lo activa, le da los límites y se entera de su derrota con el evento Derrotado.
[RequireComponent(typeof(Rigidbody2D))]
public class JefeCentinela : JefeBase
{
    public enum Estado { Inactivo, Idle, Caminar, Ataque, Aturdido, Alarma, Muerte }
    private enum TipoAtaque { Ninguno, Rafaga, Embestida, Drones }

    [Header("Nombre (barra de vida)")]
    [SerializeField] private string nombre = "Centinela de Acceso";
    [Tooltip("Hacia dónde mira mientras espera al jugador (la entrada de la arena está a la izquierda)")]
    [SerializeField] private bool mirarIzquierdaAlInicio = true;

    [Header("Vida")]
    [Tooltip("Misma escala que el resto: una bala del jugador hace 10, un Dron tiene 30 (3 balas). 150 = 15 balas")]
    [SerializeField] private float vidaMaxima = 150f;
    [Tooltip("Multiplicador de daño recibido normalmente")]
    [SerializeField] private float multiplicadorNormal = 1f;
    [Tooltip("Multiplicador de daño mientras está aturdido (panel de refrigeración expuesto)")]
    [SerializeField] private float multiplicadorAturdido = 2f;
    [Tooltip("Multiplicador extra para el daño de explosiones (barriles de combustible)")]
    [SerializeField] private float multiplicadorExplosiones = 1f;

    [Header("Fases")]
    [Range(0f, 1f)] [SerializeField] private float umbralFase2 = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float umbralBlindajeRoto = 0.2f;
    [Tooltip("Daño extra recibido con el blindaje roto (1.25 = +25%)")]
    [SerializeField] private float multiplicadorBlindajeRoto = 1.25f;
    [Tooltip("Velocidad en fase 2 (1.35 = +35%)")]
    [SerializeField] private float multiplicadorVelocidadFase2 = 1.35f;
    [Tooltip("Cooldowns en fase 2 (0.7 = -30%)")]
    [SerializeField] private float multiplicadorCooldownFase2 = 0.7f;
    [SerializeField] private Color tinteFase2 = new Color(1f, 0.55f, 0.5f);
    [Tooltip("Pulsos del tinte rojo por segundo")]
    [SerializeField] private float pulsoFase2 = 1.6f;

    [Header("Movimiento")]
    [SerializeField] private float velocidadCaminar = 1.8f;
    [Tooltip("Se acerca caminando hasta esta distancia horizontal del jugador")]
    [SerializeField] private float distanciaCaminar = 6f;
    [Tooltip("Tiempo máximo caminando antes de volver a elegir ataque")]
    [SerializeField] private float tiempoMaxCaminar = 1.6f;
    [Tooltip("Pausa en Idle entre un ataque y el siguiente")]
    [SerializeField] private float pausaEntreAtaques = 0.9f;

    [Header("Ataque 1: Ráfaga de ametralladora")]
    [SerializeField] private GameObject prefabBala;
    [Tooltip("Igual que el Robot Pesado (EnemyRobot.bulletSpeed = 20). No cambia en la fase 2")]
    [SerializeField] private float velocidadBala = 20f;
    [SerializeField] private float danioBala = 10f;
    [SerializeField] private int balasPorRafaga = 6;
    [SerializeField] private float intervaloBalas = 0.24f;
    [SerializeField] private float cooldownRafaga = 3f;
    [Tooltip("Parte de la animación de disparo (0-1) en la que sale la primera bala: antes de eso levanta el arma (aviso)")]
    [Range(0f, 1f)] [SerializeField] private float inicioFuegoEnAnimacion = 0.36f;
    [Tooltip("Boca del arma mirando a la derecha, relativa a los pies del jefe. Ráfaga ALTA (se esquiva agachándose)")]
    [SerializeField] private Vector2 bocaAlta = new Vector2(2.2f, 2.75f);
    [Tooltip("Ráfaga BAJA (se esquiva saltando): altura de las balas sobre los pies")]
    [SerializeField] private Vector2 bocaBaja = new Vector2(2.2f, 0.6f);
    [Tooltip("Probabilidad de que la ráfaga sea baja")]
    [Range(0f, 1f)] [SerializeField] private float probabilidadRafagaBaja = 0.4f;

    [Header("Ataque 2: Embestida")]
    [Tooltip("Duración del aviso (ojo y luz de alarma en rojo)")]
    [SerializeField] private float duracionAvisoEmbestida = 0.8f;
    [SerializeField] private float velocidadEmbestida = 11f;
    [Tooltip("Cuánto sigue de largo más allá de donde estaba el jugador si no choca con la pared")]
    [SerializeField] private float pasadoDelJugador = 4f;
    [SerializeField] private float danioEmbestida = 25f;
    [SerializeField] private Vector2 empujeEmbestida = new Vector2(9f, 6f);
    [SerializeField] private float cooldownEmbestida = 4.5f;
    [Tooltip("Recuperación al detenerse sin chocar")]
    [SerializeField] private float recuperacionEmbestida = 0.7f;
    [SerializeField] private float duracionAturdido = 3f;
    [Tooltip("Zona que golpea al embestir (mirando a la derecha, relativa a los pies). Su borde superior marca la altura que hay que superar saltando")]
    [SerializeField] private Vector2 golpeCentro = new Vector2(1.2f, 1f);
    [SerializeField] private Vector2 golpeTamano = new Vector2(2.6f, 2f);

    [Header("Ataque 3: Llamado de drones (fase 2)")]
    [SerializeField] private GameObject prefabDron;
    [SerializeField] private int maxDrones = 2;
    [SerializeField] private float cooldownDrones = 12f;
    [Tooltip("Parte de la animación de alarma (0-1) en la que aparecen los drones")]
    [Range(0f, 1f)] [SerializeField] private float momentoDronesEnAnimacion = 0.55f;
    [Tooltip("Altura (sobre el suelo) a la que aparecen los drones: por encima de la cámara de la arena")]
    [SerializeField] private float alturaAparicionDrones = 13f;
    [Tooltip("Distancia horizontal al jugador a la que aparece cada dron")]
    [SerializeField] private float separacionDrones = 6f;

    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("Collider del cuerpo (recibe balas, choca con el suelo)")]
    [SerializeField] private Collider2D colliderCuerpo;
    [Tooltip("Hitbox del panel de refrigeración en la espalda (solo activo aturdido)")]
    [SerializeField] private JefePanelDebil panelDebil;
    [SerializeField] private ParticleSystem chispasAturdido;
    [SerializeField] private ParticleSystem blindajeRoto;
    [SerializeField] private ParticleSystem humoBlindaje;
    [SerializeField] private GameObject prefabExplosionMuerte;

    [Header("Sonidos (opcionales)")]
    [SerializeField] private AudioClip sonidoDisparo;
    [SerializeField] private AudioClip sonidoAvisoEmbestida;
    [SerializeField] private AudioClip sonidoEmbestida;
    [SerializeField] private AudioClip sonidoChoque;
    [SerializeField] private AudioClip sonidoAturdido;
    [SerializeField] private AudioClip sonidoAlarma;
    [SerializeField] private AudioClip sonidoMuerte;

    // Eventos para la arena y la barra de vida

    public override string Nombre => nombre;
    public override float Vida => vida;
    public override float VidaMaxima => vidaMaxima;
    public Estado EstadoActual => estado;
    public override bool EnFase2 => fase2;

    // Parámetros del Animator
    private static readonly int PCaminando = Animator.StringToHash("Caminando");
    private static readonly int PEmbistiendo = Animator.StringToHash("Embistiendo");
    private static readonly int PAturdido = Animator.StringToHash("Aturdido");
    private static readonly int PDisparar = Animator.StringToHash("Disparar");
    private static readonly int PAvisoEmbestida = Animator.StringToHash("AvisoEmbestida");
    private static readonly int PAlarma = Animator.StringToHash("Alarma");
    private static readonly int PMorir = Animator.StringToHash("Morir");
    private static readonly int PReiniciar = Animator.StringToHash("Reiniciar");

    private static readonly int IdFlash = Shader.PropertyToID("_FlashAmount");
    private static readonly int IdTinte = Shader.PropertyToID("_Tint");

    private Rigidbody2D rb;
    private AudioSource audioLoop;
    private MaterialPropertyBlock mpb;
    private Transform jugador;
    private Collider2D colJugador;

    private Estado estado = Estado.Inactivo;
    private float vida;
    private bool fase2;
    private bool blindajeRotoActivo;
    private bool mirandoIzquierda = true;
    private float flash;
    private float limiteIzq = float.NegativeInfinity, limiteDer = float.PositiveInfinity;
    private Vector3 posicionInicial;

    private TipoAtaque ultimoAtaque = TipoAtaque.Ninguno;
    private int repeticiones;
    private readonly Dictionary<TipoAtaque, float> listoEn = new Dictionary<TipoAtaque, float>();
    private readonly List<GameObject> drones = new List<GameObject>();
    // Zumbidos que ya empezaron pero cuyo dron todavía no aparece
    private readonly List<SonidoAcercamiento> sonidosPendientes = new List<SonidoAcercamiento>();
    private Coroutine rutina;

    private float duracionDisparo = 2f, duracionAlarma = 2f;

    private float MultVelocidad => fase2 ? multiplicadorVelocidadFase2 : 1f;
    private float MultCooldown => fase2 ? multiplicadorCooldownFase2 : 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (colliderCuerpo == null) colliderCuerpo = GetComponent<Collider2D>();
        if (panelDebil == null) panelDebil = GetComponentInChildren<JefePanelDebil>(true);
        if (panelDebil != null) panelDebil.Configurar(this);

        audioLoop = gameObject.AddComponent<AudioSource>();
        audioLoop.playOnAwake = false;
        audioLoop.spatialBlend = 0f;

        mpb = new MaterialPropertyBlock();
        posicionInicial = transform.position;
        LeerDuracionesDeClips();
        ReiniciarValores();
        Mirar(mirarIzquierdaAlInicio);
        // Mientras espera no lo empuja nada (enemigos, explosiones)
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void FixedUpdate()
    {
        // Nunca sale de la arena aunque algo lo empuje
        if (estado == Estado.Inactivo || estado == Estado.Muerte || float.IsInfinity(limiteIzq)) return;
        float mitad = colliderCuerpo != null ? colliderCuerpo.bounds.extents.x : 1.5f;
        float x = Mathf.Clamp(rb.position.x, limiteIzq + mitad, limiteDer - mitad);
        if (!Mathf.Approximately(x, rb.position.x)) rb.position = new Vector2(x, rb.position.y);
    }

    private void Start()
    {
        // Las plataformas son para el jugador: el jefe pasa por debajo/a través de ellas
        if (colliderCuerpo == null) return;
        foreach (PlatformEffector2D pe in FindObjectsByType<PlatformEffector2D>())
            foreach (Collider2D c in pe.GetComponents<Collider2D>())
                Physics2D.IgnoreCollision(colliderCuerpo, c, true);
    }

    // Duraciones reales de las animaciones (para sincronizar balas y drones con lo que se ve)
    private void LeerDuracionesDeClips()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimationClip c in animator.runtimeAnimatorController.animationClips)
        {
            if (c.name.EndsWith("Disparo")) duracionDisparo = c.length;
            else if (c.name.EndsWith("Alarma")) duracionAlarma = c.length;
        }
    }

    private void ReiniciarValores()
    {
        vida = vidaMaxima;
        fase2 = false;
        blindajeRotoActivo = false;
        ultimoAtaque = TipoAtaque.Ninguno;
        repeticiones = 0;
        listoEn.Clear();
        if (panelDebil != null) panelDebil.Activar(false);
        if (chispasAturdido != null) chispasAturdido.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (humoBlindaje != null) humoBlindaje.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        flash = 0f;
        AplicarMaterial();
    }

    // ---------------------------------------------------------------- API para la arena

    // Límites en X de la arena: si choca con ellos durante la embestida, queda aturdido
    public override void ConfigurarArena(float izquierda, float derecha)
    {
        limiteIzq = izquierda;
        limiteDer = derecha;
    }

    public override void Activar(Transform objetivo)
    {
        if (estado != Estado.Inactivo) return;
        jugador = objetivo;
        colJugador = objetivo != null ? objetivo.GetComponent<Collider2D>() : null;
        // El jugador lo atraviesa; el daño por contacto solo lo hace la embestida
        if (colJugador != null && colliderCuerpo != null) Physics2D.IgnoreCollision(colliderCuerpo, colJugador, true);
        rb.bodyType = RigidbodyType2D.Dynamic;

        // Primer ataque un poco después de aparecer
        listoEn[TipoAtaque.Rafaga] = Time.time + 1.2f;
        listoEn[TipoAtaque.Embestida] = Time.time + 2.5f;
        listoEn[TipoAtaque.Drones] = Time.time;
        AvisarVida(vida, vidaMaxima);
        CambiarRutina(RutinaIdle(0.8f));
    }

    // Vuelve al estado inicial (el jugador murió y reaparece en el checkpoint)
    public override void Reiniciar()
    {
        if (rutina != null) StopCoroutine(rutina);
        rutina = null;
        DestruirDrones(false);
        audioLoop.Stop();
        estado = Estado.Inactivo;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.position = posicionInicial;
        transform.position = posicionInicial;
        if (colliderCuerpo != null) colliderCuerpo.enabled = true;
        ReiniciarValores();
        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetBool(PCaminando, false);
            animator.SetBool(PEmbistiendo, false);
            animator.SetBool(PAturdido, false);
            animator.ResetTrigger(PMorir);
            animator.SetTrigger(PReiniciar);
        }
        Mirar(mirarIzquierdaAlInicio);
        AvisarVida(vida, vidaMaxima);
    }

    // ---------------------------------------------------------------- Daño

    public override void TakeDamage(float amount) => RecibirDanio(amount, false);

    // Explosiones de barriles: daño alto, con su propio multiplicador
    public override void RecibirDanioExplosion(float amount) => RecibirDanio(amount * multiplicadorExplosiones, false);

    // Lo llama el panel de la espalda (solo activo mientras está aturdido)
    public void RecibirDanioPanel(float amount) => RecibirDanio(amount, true);

    private void RecibirDanio(float amount, bool desdePanel)
    {
        // Antes de activarse y ya muerto es invulnerable
        if (estado == Estado.Inactivo || estado == Estado.Muerte) return;

        float mult = estado == Estado.Aturdido ? multiplicadorAturdido : multiplicadorNormal;
        if (blindajeRotoActivo) mult *= multiplicadorBlindajeRoto;

        vida = Mathf.Max(0f, vida - amount * mult);
        flash = 1f;
        AvisarVida(vida, vidaMaxima);

        if (vida <= 0f) { Morir(); return; }

        if (!blindajeRotoActivo && vida <= vidaMaxima * umbralBlindajeRoto) RomperBlindaje();

        // La transición a fase 2 es inmediata (interrumpe lo que esté haciendo)
        if (!fase2 && vida <= vidaMaxima * umbralFase2)
        {
            fase2 = true;
            CambiarRutina(RutinaTransicionFase2());
        }
    }

    private void RomperBlindaje()
    {
        blindajeRotoActivo = true;
        if (blindajeRoto != null) blindajeRoto.Play(true);
        if (humoBlindaje != null) humoBlindaje.Play(true);
        CameraFollow.Shake(0.2f, 0.25f);
    }

    private void Morir()
    {
        CambiarRutina(null);
        estado = Estado.Muerte;
        audioLoop.Stop();
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (colliderCuerpo != null) colliderCuerpo.enabled = false;
        if (panelDebil != null) panelDebil.Activar(false);
        if (chispasAturdido != null) chispasAturdido.Stop();
        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetBool(PCaminando, false);
            animator.SetBool(PEmbistiendo, false);
            animator.SetBool(PAturdido, false);
            animator.SetTrigger(PMorir);
        }
        DestruirDrones(true);
        SoundFX.Play(sonidoMuerte);
        CameraFollow.Shake(0.45f, 0.6f);
        if (prefabExplosionMuerte != null && colliderCuerpo != null)
            Instantiate(prefabExplosionMuerte, colliderCuerpo.bounds.center, Quaternion.identity);
        AvisarDerrota();
    }

    // ---------------------------------------------------------------- Ciclo

    private void Update()
    {
        // Parpadeo blanco al recibir daño y pulso rojo en fase 2
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 10f);
        AplicarMaterial();
    }

    private void AplicarMaterial()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(IdFlash, flash);
        Color tinte = Color.white;
        if (fase2 && estado != Estado.Muerte)
        {
            float k = Mathf.Sin(Time.time * pulsoFase2 * Mathf.PI * 2f) * 0.5f + 0.5f;
            tinte = Color.Lerp(Color.Lerp(Color.white, tinteFase2, 0.45f), tinteFase2, k);
        }
        mpb.SetColor(IdTinte, tinte);
        spriteRenderer.SetPropertyBlock(mpb);
    }

    private void CambiarRutina(IEnumerator nueva)
    {
        if (rutina != null) StopCoroutine(rutina);
        rutina = null;
        // Limpia lo que pudo dejar a medias la rutina anterior
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        audioLoop.Stop();
        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetBool(PCaminando, false);
            animator.SetBool(PEmbistiendo, false);
            animator.SetBool(PAturdido, false);
        }
        if (panelDebil != null) panelDebil.Activar(false);
        if (chispasAturdido != null) chispasAturdido.Stop();
        ApagarSonidosPendientes();   // una alarma interrumpida ya no trae drones
        if (nueva != null) rutina = StartCoroutine(nueva);
    }

    // ---------------------------------------------------------------- Estados

    private IEnumerator RutinaIdle(float pausa)
    {
        estado = Estado.Idle;
        float fin = Time.time + pausa;
        while (Time.time < fin)
        {
            MirarJugador();
            yield return null;
        }

        TipoAtaque ataque = ElegirAtaque();
        if (ataque == TipoAtaque.Ninguno) { rutina = StartCoroutine(RutinaCaminar()); yield break; }

        RegistrarAtaque(ataque);
        switch (ataque)
        {
            case TipoAtaque.Rafaga: rutina = StartCoroutine(RutinaRafaga()); break;
            case TipoAtaque.Embestida: rutina = StartCoroutine(RutinaEmbestida()); break;
            case TipoAtaque.Drones: rutina = StartCoroutine(RutinaDrones()); break;
        }
    }

    private IEnumerator RutinaCaminar()
    {
        estado = Estado.Caminar;
        float fin = Time.time + tiempoMaxCaminar;
        if (animator != null) animator.SetBool(PCaminando, true);

        while (Time.time < fin && jugador != null)
        {
            MirarJugador();
            float dx = jugador.position.x - transform.position.x;
            if (Mathf.Abs(dx) <= distanciaCaminar) break;
            float vx = Mathf.Sign(dx) * velocidadCaminar * MultVelocidad;
            // No se mete en la pared
            if ((vx < 0f && transform.position.x <= limiteIzq + 2f) || (vx > 0f && transform.position.x >= limiteDer - 2f)) break;
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            yield return null;
        }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animator != null) animator.SetBool(PCaminando, false);
        rutina = StartCoroutine(RutinaIdle(0.25f));
    }

    // Elige por distancia y cooldown, sin repetir el mismo ataque más de 2 veces seguidas.
    // Fase 1: siempre el de mayor prioridad (predecible). Fase 2: al azar ponderado.
    private TipoAtaque ElegirAtaque()
    {
        float distancia = jugador != null ? Mathf.Abs(jugador.position.x - transform.position.x) : 0f;
        var opciones = new List<(TipoAtaque tipo, float peso)>();

        void Agregar(TipoAtaque t, float peso)
        {
            if (peso <= 0f || Time.time < ListoEn(t)) return;
            if (t == ultimoAtaque && repeticiones >= 2) return;
            opciones.Add((t, peso));
        }

        // De lejos prefiere la ráfaga; de cerca, la embestida
        Agregar(TipoAtaque.Rafaga, distancia >= 7f ? 3f : 1.5f);
        Agregar(TipoAtaque.Embestida, distancia >= 7f ? 2f : 3f);
        if (fase2 && prefabDron != null && DronesVivos() < maxDrones) Agregar(TipoAtaque.Drones, 4f);

        // Nada listo: camina hacia el jugador (si ya está cerca, espera a que se libere un ataque)
        if (opciones.Count == 0) return TipoAtaque.Ninguno;

        if (!fase2)
        {
            // Predecible: mayor peso; en empate, el que no se usó la vez anterior
            opciones.Sort((a, b) =>
            {
                int c = b.peso.CompareTo(a.peso);
                if (c != 0) return c;
                return (a.tipo == ultimoAtaque).CompareTo(b.tipo == ultimoAtaque);
            });
            return opciones[0].tipo;
        }

        float total = 0f;
        foreach (var o in opciones) total += o.peso;
        float r = UnityEngine.Random.value * total;
        foreach (var o in opciones)
        {
            r -= o.peso;
            if (r <= 0f) return o.tipo;
        }
        return opciones[opciones.Count - 1].tipo;
    }

    private float ListoEn(TipoAtaque t) => listoEn.TryGetValue(t, out float v) ? v : 0f;

    private void RegistrarAtaque(TipoAtaque t)
    {
        repeticiones = t == ultimoAtaque ? repeticiones + 1 : 1;
        ultimoAtaque = t;
    }

    private void PonerCooldown(TipoAtaque t, float segundos) => listoEn[t] = Time.time + segundos * MultCooldown;

    // ---------------------------------------------------------------- Ataque 1: Ráfaga

    private IEnumerator RutinaRafaga()
    {
        estado = Estado.Ataque;
        MirarJugador();
        bool baja = UnityEngine.Random.value < probabilidadRafagaBaja;
        if (animator != null) animator.SetTrigger(PDisparar);

        // Aviso: levanta el arma (primeros cuadros de la animación, sin fogonazo)
        float duracion = duracionDisparo / MultVelocidad;
        if (animator != null) animator.speed = MultVelocidad;
        yield return new WaitForSeconds(duracion * inicioFuegoEnAnimacion);

        SoundFX.Play(sonidoDisparo);
        int balas = Mathf.Max(1, balasPorRafaga);
        float intervalo = intervaloBalas / MultVelocidad;
        for (int i = 0; i < balas; i++)
        {
            Disparar(baja ? bocaBaja : bocaAlta);
            yield return new WaitForSeconds(intervalo);
        }

        // Termina la animación
        float resto = duracion * (1f - inicioFuegoEnAnimacion) - balas * intervalo;
        if (resto > 0f) yield return new WaitForSeconds(resto);
        if (animator != null) animator.speed = 1f;

        PonerCooldown(TipoAtaque.Rafaga, cooldownRafaga);
        rutina = StartCoroutine(RutinaIdle(pausaEntreAtaques * MultCooldown));
    }

    private void Disparar(Vector2 boca)
    {
        if (prefabBala == null) return;
        float lado = mirandoIzquierda ? -1f : 1f;
        Vector3 pos = transform.position + new Vector3(boca.x * lado, boca.y, 0f);
        GameObject b = Instantiate(prefabBala, pos, Quaternion.identity);
        Bullet bala = b.GetComponent<Bullet>();
        if (bala != null)
        {
            bala.SetIsEnemyBullet(true);
            bala.SetSpeed(velocidadBala);
            bala.SetDamage(danioBala);
            bala.SetDirection(new Vector2(lado, 0f));
        }
    }

    // ---------------------------------------------------------------- Ataque 2: Embestida

    private IEnumerator RutinaEmbestida()
    {
        estado = Estado.Ataque;
        MirarJugador();
        float dir = mirandoIzquierda ? -1f : 1f;

        // Aviso: ojo y luz de alarma en rojo
        if (animator != null) animator.SetTrigger(PAvisoEmbestida);
        SoundFX.Play(sonidoAvisoEmbestida);
        yield return new WaitForSeconds(duracionAvisoEmbestida / Mathf.Lerp(1f, MultVelocidad, 0.5f));

        // Carrera en línea recta: hasta un poco más allá de donde estaba el jugador o hasta la pared
        float objetivoX = jugador != null ? jugador.position.x + dir * pasadoDelJugador : transform.position.x + dir * 8f;
        float mitadAncho = colliderCuerpo != null ? colliderCuerpo.bounds.extents.x : 1.5f;
        float pared = dir > 0f ? limiteDer - mitadAncho : limiteIzq + mitadAncho;
        bool chocara = dir > 0f ? objetivoX >= pared : objetivoX <= pared;
        float finX = chocara ? pared : objetivoX;

        if (animator != null) animator.SetBool(PEmbistiendo, true);
        SoundFX.Play(sonidoEmbestida);
        bool golpeo = false;
        float velocidad = velocidadEmbestida * MultVelocidad;
        float limiteTiempo = Time.time + 4f;   // por si algo lo bloquea

        while ((finX - transform.position.x) * dir > 0.05f && Time.time < limiteTiempo)
        {
            rb.linearVelocity = new Vector2(dir * velocidad, rb.linearVelocity.y);
            if (!golpeo) golpeo = GolpearConEmbestida(dir);
            yield return new WaitForFixedUpdate();
        }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animator != null) animator.SetBool(PEmbistiendo, false);
        PonerCooldown(TipoAtaque.Embestida, cooldownEmbestida);
        ReventarBarrilesBajoElCuerpo();

        if (chocara)
        {
            AvisarChoque(dir);
            rutina = StartCoroutine(RutinaAturdido());
            yield break;
        }

        // Se detiene con una breve recuperación
        yield return new WaitForSeconds(recuperacionEmbestida);
        rutina = StartCoroutine(RutinaIdle(pausaEntreAtaques * 0.5f * MultCooldown));
    }

    // Daña y empuja al jugador una sola vez por embestida; también revienta barriles en el camino
    private bool GolpearConEmbestida(float dir)
    {
        Vector2 centro = (Vector2)transform.position + new Vector2(golpeCentro.x * dir, golpeCentro.y);
        bool golpeo = false;
        // Todas las capas: los barriles pueden estar en cualquiera
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(centro, golpeTamano, 0f, Physics2D.AllLayers))
        {
            if (hit.CompareTag("Player"))
            {
                var hp = hit.GetComponent<PlayerHealth>();
                if (hp == null || hp.EstaMuerto) continue;
                hp.TakeDamage(danioEmbestida);
                var prb = hit.attachedRigidbody;
                if (prb != null) prb.linearVelocity = new Vector2(dir * empujeEmbestida.x, empujeEmbestida.y);
                CameraFollow.Shake(0.25f, 0.25f);
                golpeo = true;
            }
            else
            {
                var barril = hit.GetComponent<BarrilExplosivo>();
                if (barril != null) barril.Explotar();
            }
        }
        return golpeo;
    }

    // Al frenar (sobre todo contra la pared) puede quedar encima de un barril que el golpe frontal no alcanzó
    private void ReventarBarrilesBajoElCuerpo()
    {
        if (colliderCuerpo == null) return;
        Bounds b = colliderCuerpo.bounds;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(b.center, b.size, 0f, Physics2D.AllLayers))
        {
            var barril = hit.GetComponent<BarrilExplosivo>();
            if (barril != null) barril.Explotar();
        }
    }

    private IEnumerator RutinaAturdido()
    {
        estado = Estado.Aturdido;
        SoundFX.Play(sonidoChoque);
        CameraFollow.Shake(0.4f, 0.4f);
        if (animator != null) animator.SetBool(PAturdido, true);
        if (panelDebil != null) panelDebil.Activar(true);
        if (chispasAturdido != null) chispasAturdido.Play(true);
        if (sonidoAturdido != null) { audioLoop.clip = sonidoAturdido; audioLoop.loop = true; audioLoop.Play(); }

        // Tiembla en el sitio mientras está aturdido
        Transform visual = spriteRenderer != null ? spriteRenderer.transform : null;
        Vector3 basePos = visual != null ? visual.localPosition : Vector3.zero;
        float fin = Time.time + duracionAturdido;
        while (Time.time < fin)
        {
            if (visual != null) visual.localPosition = basePos + new Vector3(Mathf.Sin(Time.time * 60f) * 0.04f, 0f, 0f);
            yield return null;
        }
        if (visual != null) visual.localPosition = basePos;

        audioLoop.Stop();
        audioLoop.loop = false;
        if (chispasAturdido != null) chispasAturdido.Stop();
        if (panelDebil != null) panelDebil.Activar(false);
        if (animator != null) animator.SetBool(PAturdido, false);
        rutina = StartCoroutine(RutinaIdle(pausaEntreAtaques * MultCooldown));
    }

    // ---------------------------------------------------------------- Ataque 3 y fase 2: Alarma

    private IEnumerator RutinaDrones()
    {
        estado = Estado.Alarma;
        MirarJugador();
        if (animator != null) animator.SetTrigger(PAlarma);
        SoundFX.Play(sonidoAlarma);

        // El zumbido de los drones empieza con la alarma, antes de que aparezcan (desde donde van a salir)
        int faltan = maxDrones - DronesVivos();
        var posiciones = new Vector3[faltan];
        var sonidos = new SonidoAcercamiento[faltan];
        sonidosPendientes.Clear();
        var dronPrefab = prefabDron != null ? prefabDron.GetComponent<DroneEnemy>() : null;
        for (int i = 0; i < faltan; i++)
        {
            posiciones[i] = PosicionDron(i);
            if (dronPrefab != null)
                sonidos[i] = SonidoAcercamiento.Crear(dronPrefab.AproximacionSound, posiciones[i], dronPrefab.AproximacionVolumen);
            if (sonidos[i] != null) sonidosPendientes.Add(sonidos[i]);
        }

        yield return new WaitForSeconds(duracionAlarma * momentoDronesEnAnimacion);

        for (int i = 0; i < faltan; i++) InvocarDron(posiciones[i], sonidos[i]);
        sonidosPendientes.Clear();

        yield return new WaitForSeconds(duracionAlarma * (1f - momentoDronesEnAnimacion));
        PonerCooldown(TipoAtaque.Drones, cooldownDrones);
        rutina = StartCoroutine(RutinaIdle(pausaEntreAtaques * MultCooldown));
    }

    private IEnumerator RutinaTransicionFase2()
    {
        estado = Estado.Alarma;
        if (animator != null) animator.SetTrigger(PAlarma);
        SoundFX.Play(sonidoAlarma);
        CameraFollow.Shake(0.3f, 0.6f);
        yield return new WaitForSeconds(duracionAlarma);
        // Los drones quedan disponibles de inmediato en la fase 2
        listoEn[TipoAtaque.Drones] = Time.time;
        rutina = StartCoroutine(RutinaIdle(0.4f));
    }

    // Entran desde arriba (fuera de cámara), uno a cada lado del jugador y lo bastante cerca
    // para que lo detecten (el Dron detecta a 16 unidades)
    private Vector3 PosicionDron(int indice)
    {
        float baseX = jugador != null ? jugador.position.x : transform.position.x;
        float x = baseX + (indice % 2 == 0 ? -separacionDrones : separacionDrones);
        if (!float.IsInfinity(limiteIzq)) x = Mathf.Clamp(x, limiteIzq + 1.5f, limiteDer - 1.5f);
        return new Vector3(x, transform.position.y + alturaAparicionDrones, 0f);
    }

    private void InvocarDron(Vector3 pos, SonidoAcercamiento sonido)
    {
        GameObject d = Instantiate(prefabDron, pos, Quaternion.identity);
        var dron = d.GetComponent<DroneEnemy>();
        if (dron != null && sonido != null) dron.UsarSonidoAcercamiento(sonido);
        drones.Add(d);
    }

    private void ApagarSonidosPendientes()
    {
        foreach (SonidoAcercamiento s in sonidosPendientes) if (s != null) s.Apagar();
        sonidosPendientes.Clear();
    }

    private int DronesVivos()
    {
        drones.RemoveAll(d => d == null);
        return drones.Count;
    }

    // Con 'explotar' los drones mueren con su explosión normal; sin él desaparecen sin más
    private void DestruirDrones(bool explotar)
    {
        ApagarSonidosPendientes();
        foreach (GameObject d in drones)
        {
            if (d == null) continue;
            var dano = d.GetComponent<IDamageable>();
            if (explotar && dano != null) dano.TakeDamage(99999f);
            else Destroy(d);
        }
        drones.Clear();
    }

    // ---------------------------------------------------------------- Utilidades

    private void MirarJugador()
    {
        if (jugador == null) return;
        float dx = jugador.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.3f) Mirar(dx < 0f);
    }

    // Los sprites miran a la derecha: flipX cuando mira a la izquierda. El panel débil va siempre en la espalda.
    private void Mirar(bool izquierda)
    {
        mirandoIzquierda = izquierda;
        if (spriteRenderer != null) spriteRenderer.flipX = izquierda;
        if (panelDebil != null) panelDebil.Orientar(izquierda);
    }

    private void OnDrawGizmosSelected()
    {
        float lado = Application.isPlaying && mirandoIzquierda ? -1f : 1f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + new Vector3(bocaAlta.x * lado, bocaAlta.y), 0.15f);
        Gizmos.color = new Color(1f, 0.6f, 0f);
        Gizmos.DrawWireSphere(transform.position + new Vector3(bocaBaja.x * lado, bocaBaja.y), 0.15f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + new Vector3(golpeCentro.x * lado, golpeCentro.y), golpeTamano);
    }
}
