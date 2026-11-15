using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Jefe del Nivel 2: "Dron Volador Reforzado" (versión provisional sobre una imagen fija; las
// animaciones se simulan por código: balanceo, inclinación, temblores, parpadeos).
// Estados: Inactivo → Vuelo → Ataque (Ráfaga / Picada / Llamado) → Recalentado → Muerte.
//   - Ráfaga: aviso (ojo rojo) y dispara desde sus dos cañones apuntando al jugador.
//   - Picada: aviso (tiembla) y se lanza en diagonal hacia donde está el jugador; al tocar abajo
//     queda RECALENTADO unos segundos cerca del suelo, recibiendo el doble de daño.
//   - Llamado (solo fase 2): suelta robots voladores por la escotilla (máximo 2 vivos).
// Fase 2 (50%): alarma, tinte rojo pulsante, +35% velocidad, -30% cooldowns. Al 20%: blindaje roto (+25% daño).
[RequireComponent(typeof(Rigidbody2D))]
public class JefeDronVolador : JefeBase
{
    public enum Estado { Inactivo, Vuelo, Ataque, Recalentado, Alarma, Muerte }
    private enum TipoAtaque { Ninguno, Rafaga, Picada, Llamado }

    [Header("Nombre (barra de vida)")]
    [SerializeField] private string nombre = "Dron Volador Reforzado";

    [Header("Vida")]
    [Tooltip("Una bala del jugador hace 10. 200 = 20 balas")]
    [SerializeField] private float vidaMaxima = 200f;
    [SerializeField] private float multiplicadorRecalentado = 2f;
    [SerializeField] private float multiplicadorExplosiones = 1f;

    [Header("Fases")]
    [Range(0f, 1f)] [SerializeField] private float umbralFase2 = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float umbralBlindajeRoto = 0.2f;
    [SerializeField] private float multiplicadorBlindajeRoto = 1.25f;
    [SerializeField] private float multiplicadorVelocidadFase2 = 1.35f;
    [SerializeField] private float multiplicadorCooldownFase2 = 0.7f;
    [SerializeField] private Color tinteFase2 = new Color(1f, 0.55f, 0.5f);
    [SerializeField] private float pulsoFase2 = 1.6f;

    [Header("Vuelo")]
    [Tooltip("Altura del centro del dron sobre el suelo mientras patrulla")]
    [SerializeField] private float alturaVuelo = 5.6f;
    [SerializeField] private float velocidadVuelo = 3.5f;
    [Tooltip("Distancia a las paredes de la arena que no cruza")]
    [SerializeField] private float margenParedes = 3.2f;
    [SerializeField] private float balanceoAmplitud = 0.25f;
    [SerializeField] private float balanceoVelocidad = 1.6f;
    [Tooltip("Inclinación máxima (grados) al moverse de lado")]
    [SerializeField] private float inclinacionMaxima = 9f;
    [SerializeField] private float tiempoVueloEntreAtaques = 1.4f;
    [SerializeField] private float sueloY = 0f;

    [Header("Ataque 1: Ráfaga")]
    [SerializeField] private GameObject prefabBala;
    [SerializeField] private float velocidadBala = 14f;
    [SerializeField] private float danioBala = 10f;
    [SerializeField] private int balasPorCanon = 3;
    [SerializeField] private float intervaloBalas = 0.18f;
    [SerializeField] private float dispersion = 4f;
    [SerializeField] private float avisoRafaga = 0.6f;
    [SerializeField] private float cooldownRafaga = 2.6f;
    [SerializeField] private Transform canonIzquierdo;
    [SerializeField] private Transform canonDerecho;

    [Header("Ataque 2: Picada")]
    [SerializeField] private float avisoPicada = 0.8f;
    [SerializeField] private float velocidadPicada = 13f;
    [Tooltip("Altura del centro del dron sobre el suelo al terminar la picada")]
    [SerializeField] private float alturaPicada = 1.9f;
    [SerializeField] private float danioPicada = 25f;
    [SerializeField] private Vector2 empujePicada = new Vector2(8f, 7f);
    [SerializeField] private float duracionRecalentado = 2.2f;
    [SerializeField] private float cooldownPicada = 5f;

    [Header("Ataque 3: Llamado de robots voladores (fase 2)")]
    [SerializeField] private GameObject prefabRobotVolador;
    [SerializeField] private int maxRobots = 2;
    [SerializeField] private float cooldownLlamado = 12f;
    [SerializeField] private Transform escotilla;

    [Header("Referencias")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D colliderCuerpo;
    [SerializeField] private ParticleSystem chispasRecalentado;
    [SerializeField] private ParticleSystem blindajeRoto;
    [SerializeField] private ParticleSystem humoBlindaje;
    [SerializeField] private GameObject prefabExplosion;

    [Header("Sonidos (opcionales)")]
    [SerializeField] private AudioClip sonidoDisparo;
    [SerializeField] private AudioClip sonidoAvisoPicada;
    [SerializeField] private AudioClip sonidoPicada;
    [SerializeField] private AudioClip sonidoImpacto;
    [SerializeField] private AudioClip sonidoAlarma;
    [SerializeField] private AudioClip sonidoMuerte;

    public override string Nombre => nombre;
    public override float Vida => vida;
    public override float VidaMaxima => vidaMaxima;
    public override bool EnFase2 => fase2;
    public Estado EstadoActual => estado;

    private static readonly int IdFlash = Shader.PropertyToID("_FlashAmount");
    private static readonly int IdTinte = Shader.PropertyToID("_Tint");

    private Rigidbody2D rb;
    private MaterialPropertyBlock mpb;
    private Transform jugador;
    private Transform visual;
    private Estado estado = Estado.Inactivo;
    private float vida;
    private bool fase2, blindajeRotoActivo;
    private bool mirandoIzquierda = true;
    private float flash, aviso;         // aviso: 0..1 rojo del ojo/cuerpo
    private float temblor;              // 0..1 vibración del sprite
    private float limiteIzq = float.NegativeInfinity, limiteDer = float.PositiveInfinity;
    private Vector3 posicionInicial;
    private Vector2 velocidadActual;
    private float faseBalanceo;
    private bool sinBalanceo;
    private TipoAtaque ultimoAtaque = TipoAtaque.Ninguno;
    private int repeticiones;
    private readonly Dictionary<TipoAtaque, float> listoEn = new Dictionary<TipoAtaque, float>();
    private readonly List<GameObject> robots = new List<GameObject>();
    private Coroutine rutina;

    private float MultVelocidad => fase2 ? multiplicadorVelocidadFase2 : 1f;
    private float MultCooldown => fase2 ? multiplicadorCooldownFase2 : 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (colliderCuerpo == null) colliderCuerpo = GetComponent<Collider2D>();
        visual = spriteRenderer != null ? spriteRenderer.transform : transform;
        mpb = new MaterialPropertyBlock();
        posicionInicial = transform.position;
        ReiniciarValores();
        Mirar(true);
    }

    private void ReiniciarValores()
    {
        vida = vidaMaxima;
        fase2 = blindajeRotoActivo = false;
        ultimoAtaque = TipoAtaque.Ninguno;
        repeticiones = 0;
        listoEn.Clear();
        flash = aviso = temblor = 0f;
        if (chispasRecalentado != null) chispasRecalentado.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (humoBlindaje != null) humoBlindaje.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // ---------------------------------------------------------------- API de la arena

    public override void ConfigurarArena(float izquierda, float derecha)
    {
        limiteIzq = izquierda;
        limiteDer = derecha;
    }

    public override void Activar(Transform objetivo)
    {
        if (estado != Estado.Inactivo) return;
        jugador = objetivo;
        listoEn[TipoAtaque.Rafaga] = Time.time + 1.5f;
        listoEn[TipoAtaque.Picada] = Time.time + 3.5f;
        listoEn[TipoAtaque.Llamado] = Time.time;
        AvisarVida(vida, vidaMaxima);
        CambiarRutina(RutinaVuelo(1.2f));
    }

    public override void Reiniciar()
    {
        CambiarRutina(null);
        DestruirRobots(false);
        estado = Estado.Inactivo;
        rb.position = posicionInicial;
        transform.position = posicionInicial;
        velocidadActual = Vector2.zero;
        sinBalanceo = false;
        if (colliderCuerpo != null) colliderCuerpo.enabled = true;
        visual.localRotation = Quaternion.identity;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        ReiniciarValores();
        Mirar(true);
        AvisarVida(vida, vidaMaxima);
    }

    // ---------------------------------------------------------------- Daño

    public override void TakeDamage(float amount) => RecibirDanio(amount);
    public override void RecibirDanioExplosion(float amount) => RecibirDanio(amount * multiplicadorExplosiones);

    private void RecibirDanio(float amount)
    {
        if (estado == Estado.Inactivo || estado == Estado.Muerte) return;
        float mult = estado == Estado.Recalentado ? multiplicadorRecalentado : 1f;
        if (blindajeRotoActivo) mult *= multiplicadorBlindajeRoto;
        vida = Mathf.Max(0f, vida - amount * mult);
        flash = 1f;
        AvisarVida(vida, vidaMaxima);

        if (vida <= 0f) { Morir(); return; }
        if (!blindajeRotoActivo && vida <= vidaMaxima * umbralBlindajeRoto)
        {
            blindajeRotoActivo = true;
            if (blindajeRoto != null) blindajeRoto.Play(true);
            if (humoBlindaje != null) humoBlindaje.Play(true);
            CameraFollow.Shake(0.2f, 0.25f);
        }
        if (!fase2 && vida <= vidaMaxima * umbralFase2)
        {
            fase2 = true;
            CambiarRutina(RutinaAlarma());
        }
    }

    private void Morir()
    {
        CambiarRutina(null);
        estado = Estado.Muerte;
        if (colliderCuerpo != null) colliderCuerpo.enabled = false;
        DestruirRobots(true);
        SoundFX.Play(sonidoMuerte);
        AvisarDerrota();
        StartCoroutine(RutinaMuerte());
    }

    // Explosiones en el aire, cae girando y estalla al tocar el suelo
    private IEnumerator RutinaMuerte()
    {
        sinBalanceo = true;
        for (int i = 0; i < 4; i++)
        {
            Explosion(transform.position + (Vector3)Random.insideUnitCircle * 1.8f, 0.7f);
            CameraFollow.Shake(0.25f, 0.25f);
            temblor = 1f;
            yield return new WaitForSeconds(0.25f);
        }
        float vy = 0f;
        float giro = mirandoIzquierda ? 1f : -1f;
        while (rb.position.y > sueloY + 0.9f)
        {
            vy -= 22f * Time.deltaTime;
            rb.MovePosition(rb.position + new Vector2(giro * -1.5f, vy) * Time.deltaTime);
            visual.Rotate(0f, 0f, giro * 140f * Time.deltaTime);
            yield return null;
        }
        Explosion(transform.position, 1.6f);
        CameraFollow.Shake(0.5f, 0.6f);
        if (spriteRenderer != null) spriteRenderer.color = new Color(0.35f, 0.32f, 0.3f);   // chatarra quemada
        if (humoBlindaje != null) humoBlindaje.Play(true);
    }

    private void Explosion(Vector3 pos, float escala)
    {
        if (prefabExplosion == null) return;
        var e = Instantiate(prefabExplosion, pos, Quaternion.identity);
        e.transform.localScale = Vector3.one * escala;
    }

    // ---------------------------------------------------------------- Ciclo

    private void Update()
    {
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 10f);
        temblor = Mathf.MoveTowards(temblor, 0f, Time.deltaTime * 2f);
        AplicarVisual();
    }

    private void FixedUpdate()
    {
        if (estado == Estado.Muerte) return;
        // Movimiento base + balanceo en el aire
        Vector2 p = rb.position + velocidadActual * Time.fixedDeltaTime;
        if (!sinBalanceo)
        {
            faseBalanceo += Time.fixedDeltaTime * balanceoVelocidad;
            p.y += (Mathf.Sin(faseBalanceo) - Mathf.Sin(faseBalanceo - Time.fixedDeltaTime * balanceoVelocidad)) * balanceoAmplitud;
        }
        if (!float.IsInfinity(limiteIzq)) p.x = Mathf.Clamp(p.x, limiteIzq + margenParedes * 0.6f, limiteDer - margenParedes * 0.6f);
        rb.MovePosition(p);
    }

    private void AplicarVisual()
    {
        if (spriteRenderer == null) return;
        // Inclinación según la velocidad horizontal, más temblor de aviso
        if (estado != Estado.Muerte)
        {
            float inclinacion = -Mathf.Clamp(velocidadActual.x / Mathf.Max(0.1f, velocidadPicada), -1f, 1f) * inclinacionMaxima;
            visual.localRotation = Quaternion.Euler(0f, 0f, inclinacion);
            visual.localPosition = temblor > 0f ? (Vector3)(Random.insideUnitCircle * 0.06f * temblor) : Vector3.zero;
        }

        spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(IdFlash, Mathf.Max(flash, aviso * (0.35f + 0.35f * Mathf.Sin(Time.time * 30f))));
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
        velocidadActual = Vector2.zero;
        aviso = 0f;
        sinBalanceo = false;
        if (chispasRecalentado != null) chispasRecalentado.Stop();
        if (nueva != null) rutina = StartCoroutine(nueva);
    }

    // ---------------------------------------------------------------- Estados

    // Patrulla de lado a lado a su altura de vuelo, luego elige ataque
    private IEnumerator RutinaVuelo(float duracion)
    {
        estado = Estado.Vuelo;
        float fin = Time.time + duracion;
        float destinoX = ElegirDestinoX();
        while (Time.time < fin)
        {
            MirarJugador();
            Vector2 destino = new Vector2(destinoX, sueloY + alturaVuelo);
            Vector2 d = destino - rb.position;
            if (Mathf.Abs(d.x) < 0.3f) destinoX = ElegirDestinoX();
            velocidadActual = Vector2.ClampMagnitude(d * 2f, velocidadVuelo * MultVelocidad);
            yield return null;
        }
        velocidadActual = Vector2.zero;

        TipoAtaque ataque = ElegirAtaque();
        if (ataque == TipoAtaque.Ninguno) { rutina = StartCoroutine(RutinaVuelo(0.6f)); yield break; }
        repeticiones = ataque == ultimoAtaque ? repeticiones + 1 : 1;
        ultimoAtaque = ataque;
        switch (ataque)
        {
            case TipoAtaque.Rafaga: rutina = StartCoroutine(RutinaRafaga()); break;
            case TipoAtaque.Picada: rutina = StartCoroutine(RutinaPicada()); break;
            case TipoAtaque.Llamado: rutina = StartCoroutine(RutinaLlamado()); break;
        }
    }

    // Se coloca a un lado del jugador (no justo encima), dentro de la arena
    private float ElegirDestinoX()
    {
        float jx = jugador != null ? jugador.position.x : transform.position.x;
        float lado = Random.value < 0.5f ? -1f : 1f;
        float x = jx + lado * Random.Range(4f, 7f);
        if (!float.IsInfinity(limiteIzq)) x = Mathf.Clamp(x, limiteIzq + margenParedes, limiteDer - margenParedes);
        return x;
    }

    private TipoAtaque ElegirAtaque()
    {
        var opciones = new List<(TipoAtaque t, float peso)>();
        void Agregar(TipoAtaque t, float peso)
        {
            if (Time.time < (listoEn.TryGetValue(t, out float v) ? v : 0f)) return;
            if (t == ultimoAtaque && repeticiones >= 2) return;
            opciones.Add((t, peso));
        }
        Agregar(TipoAtaque.Rafaga, 3f);
        Agregar(TipoAtaque.Picada, 2.5f);
        if (fase2 && prefabRobotVolador != null && RobotsVivos() < maxRobots) Agregar(TipoAtaque.Llamado, 4f);
        if (opciones.Count == 0) return TipoAtaque.Ninguno;

        if (!fase2)
        {
            // Fase 1 predecible: el de más peso que no se usó la vez anterior
            opciones.Sort((a, b) => b.peso.CompareTo(a.peso));
            foreach (var o in opciones) if (o.t != ultimoAtaque) return o.t;
            return opciones[0].t;
        }
        float total = 0f; foreach (var o in opciones) total += o.peso;
        float r = Random.value * total;
        foreach (var o in opciones) { r -= o.peso; if (r <= 0f) return o.t; }
        return opciones[opciones.Count - 1].t;
    }

    private void PonerCooldown(TipoAtaque t, float s) => listoEn[t] = Time.time + s * MultCooldown;

    // ---------------------------------------------------------------- Ráfaga

    private IEnumerator RutinaRafaga()
    {
        estado = Estado.Ataque;
        MirarJugador();
        // Aviso: el ojo se pone rojo y parpadea
        float t = 0f, dur = avisoRafaga / MultVelocidad;
        while (t < dur) { t += Time.deltaTime; aviso = Mathf.Clamp01(t / dur); yield return null; }
        aviso = 0f;

        SoundFX.Play(sonidoDisparo);
        float intervalo = intervaloBalas / MultVelocidad;
        for (int i = 0; i < balasPorCanon; i++)
        {
            Disparar(canonIzquierdo);
            Disparar(canonDerecho);
            yield return new WaitForSeconds(intervalo);
        }
        PonerCooldown(TipoAtaque.Rafaga, cooldownRafaga);
        rutina = StartCoroutine(RutinaVuelo(tiempoVueloEntreAtaques * MultCooldown));
    }

    private void Disparar(Transform canon)
    {
        if (prefabBala == null) return;
        Vector3 origen = canon != null ? canon.position : transform.position + Vector3.down;
        Vector2 objetivo = jugador != null ? (Vector2)jugador.position + Vector2.up * 1.4f : (Vector2)origen + Vector2.down;
        Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(-dispersion, dispersion)) * (objetivo - (Vector2)origen).normalized;
        GameObject b = Instantiate(prefabBala, origen, Quaternion.identity);
        var bala = b.GetComponent<Bullet>();
        if (bala != null)
        {
            bala.SetIsEnemyBullet(true);
            bala.SetSpeed(velocidadBala);
            bala.SetDamage(danioBala);
            bala.SetDirection(dir);
        }
    }

    // ---------------------------------------------------------------- Picada

    private IEnumerator RutinaPicada()
    {
        estado = Estado.Ataque;
        MirarJugador();
        SoundFX.Play(sonidoAvisoPicada);
        // Aviso: tiembla y se pone rojo
        float t = 0f, dur = avisoPicada / Mathf.Lerp(1f, MultVelocidad, 0.5f);
        while (t < dur) { t += Time.deltaTime; aviso = Mathf.Clamp01(t / dur); temblor = 1f; yield return null; }
        aviso = 0f;

        // Se lanza hacia donde estaba el jugador al terminar el aviso
        Vector2 destino = new Vector2(jugador != null ? jugador.position.x : rb.position.x, sueloY + alturaPicada);
        if (!float.IsInfinity(limiteIzq)) destino.x = Mathf.Clamp(destino.x, limiteIzq + margenParedes * 0.6f, limiteDer - margenParedes * 0.6f);
        SoundFX.Play(sonidoPicada);
        sinBalanceo = true;
        bool golpeo = false;
        float limite = Time.time + 3f;
        while (Vector2.Distance(rb.position, destino) > 0.15f && Time.time < limite)
        {
            velocidadActual = (destino - rb.position).normalized * velocidadPicada * MultVelocidad;
            if (!golpeo) golpeo = GolpearConCuerpo();
            yield return new WaitForFixedUpdate();
        }
        velocidadActual = Vector2.zero;
        SoundFX.Play(sonidoImpacto);
        CameraFollow.Shake(0.3f, 0.3f);
        PonerCooldown(TipoAtaque.Picada, cooldownPicada);

        // Recalentado: queda abajo y vulnerable
        estado = Estado.Recalentado;
        if (chispasRecalentado != null) chispasRecalentado.Play(true);
        t = 0f;
        while (t < duracionRecalentado) { t += Time.deltaTime; temblor = 0.35f; yield return null; }
        if (chispasRecalentado != null) chispasRecalentado.Stop();

        // Sube de nuevo a su altura de vuelo
        estado = Estado.Vuelo;
        while (rb.position.y < sueloY + alturaVuelo - 0.2f)
        {
            velocidadActual = new Vector2(0f, velocidadVuelo * 1.5f);
            yield return null;
        }
        sinBalanceo = false;
        rutina = StartCoroutine(RutinaVuelo(tiempoVueloEntreAtaques * MultCooldown));
    }

    // Daño por contacto durante la picada (una vez por picada)
    private bool GolpearConCuerpo()
    {
        if (colliderCuerpo == null) return false;
        Bounds b = colliderCuerpo.bounds;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(b.center, b.size, 0f, Physics2D.AllLayers))
        {
            if (hit.CompareTag("Player"))
            {
                var hp = hit.GetComponent<PlayerHealth>();
                if (hp == null || hp.EstaMuerto) continue;
                hp.TakeDamage(danioPicada);
                float lado = hit.transform.position.x >= transform.position.x ? 1f : -1f;
                if (hit.attachedRigidbody != null) hit.attachedRigidbody.linearVelocity = new Vector2(lado * empujePicada.x, empujePicada.y);
                CameraFollow.Shake(0.25f, 0.25f);
                return true;
            }
            var barril = hit.GetComponent<BarrilExplosivo>();
            if (barril != null) barril.Explotar();
        }
        return false;
    }

    // ---------------------------------------------------------------- Llamado y fase 2

    private IEnumerator RutinaLlamado()
    {
        estado = Estado.Alarma;
        SoundFX.Play(sonidoAlarma);
        float t = 0f;
        while (t < 0.7f) { t += Time.deltaTime; aviso = 0.5f + 0.5f * Mathf.Sin(t * 20f); yield return null; }
        aviso = 0f;
        int faltan = maxRobots - RobotsVivos();
        for (int i = 0; i < faltan; i++)
        {
            Vector3 p = escotilla != null ? escotilla.position : transform.position + Vector3.down;
            robots.Add(Instantiate(prefabRobotVolador, p + Vector3.right * (i == 0 ? -0.6f : 0.6f), Quaternion.identity));
            yield return new WaitForSeconds(0.35f);
        }
        PonerCooldown(TipoAtaque.Llamado, cooldownLlamado);
        rutina = StartCoroutine(RutinaVuelo(tiempoVueloEntreAtaques * MultCooldown));
    }

    private IEnumerator RutinaAlarma()
    {
        estado = Estado.Alarma;
        SoundFX.Play(sonidoAlarma);
        CameraFollow.Shake(0.3f, 0.6f);
        float t = 0f;
        while (t < 1.6f) { t += Time.deltaTime; aviso = 0.5f + 0.5f * Mathf.Sin(t * 14f); temblor = 0.6f; yield return null; }
        aviso = 0f;
        listoEn[TipoAtaque.Llamado] = Time.time;
        rutina = StartCoroutine(RutinaVuelo(0.5f));
    }

    private int RobotsVivos()
    {
        robots.RemoveAll(r => r == null);
        return robots.Count;
    }

    private void DestruirRobots(bool explotar)
    {
        foreach (GameObject r in robots)
        {
            if (r == null) continue;
            var d = r.GetComponent<IDamageable>();
            if (explotar && d != null) d.TakeDamage(99999f);
            else Destroy(r);
        }
        robots.Clear();
    }

    // ---------------------------------------------------------------- Utilidades

    private void MirarJugador()
    {
        if (jugador == null) return;
        float dx = jugador.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.3f) Mirar(dx < 0f);
    }

    // La imagen mira a la derecha (el ojo está a la derecha): flipX cuando mira a la izquierda
    private void Mirar(bool izquierda)
    {
        mirandoIzquierda = izquierda;
        if (spriteRenderer != null) spriteRenderer.flipX = izquierda;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position + Vector3.left * 3f, transform.position + Vector3.right * 3f);
        if (canonIzquierdo != null) Gizmos.DrawWireSphere(canonIzquierdo.position, 0.15f);
        if (canonDerecho != null) Gizmos.DrawWireSphere(canonDerecho.position, 0.15f);
    }
}
