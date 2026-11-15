using System;
using UnityEngine;

// Base común de los jefes. La arena (JefeArena), la barra de vida (JefeBarraVida) y los barriles
// explosivos trabajan con esta clase, así sirven para cualquier jefe (Centinela, Dron volador...).
public abstract class JefeBase : MonoBehaviour, IDamageable
{
    public event Action<float, float> VidaCambio;   // (vida actual, vida máxima)
    public event Action Derrotado;
    public event Action<float> ChocoConPared;       // dirección del choque: -1 izquierda, 1 derecha

    public abstract string Nombre { get; }
    public abstract float Vida { get; }
    public abstract float VidaMaxima { get; }
    public abstract bool EnFase2 { get; }

    // Límites en X de la arena
    public abstract void ConfigurarArena(float izquierda, float derecha);
    // Empieza la pelea contra 'objetivo' (el jugador)
    public abstract void Activar(Transform objetivo);
    // Vuelve al estado inicial (el jugador reapareció en el checkpoint)
    public abstract void Reiniciar();
    public abstract void TakeDamage(float amount);

    // Daño de explosiones (barriles). Cada jefe puede darle su propio multiplicador.
    public virtual void RecibirDanioExplosion(float amount) => TakeDamage(amount);

    // Los eventos solo se pueden lanzar desde esta clase: las subclases usan estos métodos
    protected void AvisarVida(float vida, float maxima) => VidaCambio?.Invoke(vida, maxima);
    protected void AvisarDerrota() => Derrotado?.Invoke();
    protected void AvisarChoque(float direccion) => ChocoConPared?.Invoke(direccion);
}
