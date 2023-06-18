/*
 * DHT3.c
 *
 * Created: 2023-06-16 10:36:49 AM
 *  Author: Marwa Gamal
 */ 


//#define F_CPU 16000000UL
#define F_CPU 8000000UL
#define BAUD_RATE 9600
#include <avr/io.h>
#include <util/delay.h>
#include <avr/interrupt.h>
#include "UART.h"
#define DHT_PIN 2   // Digital pin connected to the DHT sensor
#define SENSOR_PIN 0 // define the analog input pin for the MQ-2 sensor

static volatile uint8_t cmd_buffer[11];
volatile uint8_t tx_str[11] = {0};

void TX(int temp, int hum, int smoke);
void TX_AC(int status);
void TX_BZ(int status);
void Check_RX();
void AC_ON();
void AC_OFF();
void BUZZER_ON();
void BUZZER_OFF();
int GetAC();
int GetBuzzer();

int buzzer_override = 0;

void dht_start() {
	DDRD |= (1 << DHT_PIN);    // Set the data pin as an output
	PORTD &= ~(1 << DHT_PIN); // Set the data pin low
	_delay_ms(18);            // Send the start signal
	PORTD |= (1 << DHT_PIN);  // Set the data pin high
	_delay_us(40);            // Wait for at least 20 microseconds
	DDRD &= ~(1 << DHT_PIN);  // Set the data pin as an input
}

int dht_read(uint8_t* data)
{
	uint8_t bits[5] = {0};
	uint8_t cnt = 7, idx = 0; // counters // bit and byte. bit is initially set to 7, which means that the first bit read will be the most significant bit of the first byte, and byte is initially set to 0, which means that the first byte read will be stored in buffer[0].
	unsigned int loopCnt = 10000;

	while(!(PIND & (1 << DHT_PIN)))
	{
		if (loopCnt-- == 0)
		{  
			return 0;
		}
		
	}

	loopCnt = 10000;
	while(PIND & (1 << DHT_PIN))
	{
		if (loopCnt-- == 0)
		{
			return 0;
		}
		
	}

	for (int i=0; i<40; i++)
	{
		int low = 10000;
		while(!(PIND & (1 << DHT_PIN)))
		{       //_delay_us(1);
			//if (--low == 0)
			_delay_us(15);
			low -= 15;
			if(low == 0)
			{
				return 0;
			}
			
		}
		int high = 10000;
		while(PIND & (1 << DHT_PIN))
		{  //_delay_us(1);
			//if (--high == 0)
			_delay_us(15);
			high -= 15;
			if(high == 0)
			{
				return 0;
			}
			
		}
		if ((10000-high) > 40) bits[idx] |= (1 << cnt);
		if (cnt == 0)   // next byte?
		{
			cnt = 7;    // restart at MSB
			idx++;      // next byte!
		}
		else cnt--;
	}
	if (bits[4] != (bits[0] + bits[1] + bits[2] + bits[3])) {
	return 0; }
	data[0] = bits[0]; // decimal part of temperature
	data[1] = bits[1]; // integer part of temperature
	data[2] = bits[2]; // decimal part of humidity
	data[3] = bits[3]; // integer part of humidity
	return 1;
}

int main() {
	DDRD |= 1<<5 | 1<<6;
	// initialize the analog-to-digital converter (ADC)
	ADCSRA |= (1 << ADPS2) | (1 << ADPS1) | (0 << ADPS0); // set ADC prescaler to 64
	ADCSRA |= (1 << ADEN); // enable ADC
	/* Init UART driver. */
	UART_cfg my_uart_cfg;
	/* Set USART mode. */
	my_uart_cfg.UBRRL_cfg = (BAUD_RATE_VALUE)&0x00FF;
	my_uart_cfg.UBRRH_cfg = (((BAUD_RATE_VALUE)&0xFF00)>>8);
	my_uart_cfg.UCSRA_cfg = 0;
	my_uart_cfg.UCSRB_cfg = (1<<RXEN0) | (1<<TXEN0) | (1<<TXCIE0) | (1<<RXCIE0);
	my_uart_cfg.UCSRC_cfg = /*(1<<URSEL) |*/ (3<<UCSZ00);
	UART_Init(&my_uart_cfg);
	sei();
	/* Receive the full buffer command. */
	UART_ReceivePayload(cmd_buffer, 10);
  DDRD |= (1 << DHT_PIN);   // Set the data pin as an output
  PORTD |= (1 << DHT_PIN);  // Set the data pin high
	  ADMUX &= 0xF0; // clear the analog input pin selection
	  ADMUX = SENSOR_PIN; // select the sensor pin
  while (1) {
	  uint8_t data[4] = {1,2,3,0};
	  // read the analog voltage output of the MQ-2 sensor
	  ADCSRA |= (1 << ADSC); // start conversion
	  while (ADCSRA & (1 << ADSC)); // wait for conversion to finish
	  int sensorValue = ADC; // read the ADC value
	uint8_t temp, hum;
    dht_start();
    if (dht_read(data)) {
      temp = data[2]; //data[1] contains the integer part of the temperature and data[0] contains the decimal part of the temperature.
      hum = data[0];
	  if(sensorValue >= 1000)
		sensorValue = 999;
      // Use the temperature and humidity readings
	  TX(temp, hum, sensorValue);
    }
	TX_AC(GetAC());
	TX_BZ(GetBuzzer());
	_delay_ms(200);
	Check_RX();
    _delay_ms(2000);
  }
  return 0;
}

void TX(int temp, int hum, int smoke)
{
	//---------------------------------------------------------------
	sprintf(tx_str, "@%.2i%.3i%.3i;", temp, hum, smoke);
	UART_SendPayload(tx_str, 10);
	while(0 == UART_IsTxComplete());
}

void TX_AC(int status)
{
	sprintf(tx_str, "@STS:AC%i;", status!=0);
	UART_SendPayload(tx_str, 10);
	while(0 == UART_IsTxComplete());
}

void TX_BZ(int status)
{
	sprintf(tx_str, "@STS:BZ%i;", status!=0);
	UART_SendPayload(tx_str, 10);
	while(0 == UART_IsTxComplete());
}

void Check_RX()
{
	//while(0 == UART_IsRxComplete());
	if(UART_IsRxComplete())
	{
		if(!(cmd_buffer[0] == '@' && cmd_buffer[9] == ';')) return;
		if(cmd_buffer[1] == 'A')
		{
			if(cmd_buffer[2] == '0')
			{
				// AC off
				AC_OFF();
			}
			else
			{
				// AC on
				AC_ON();
			}
		}
		else
		if(cmd_buffer[1] == 'B')
		{
			
			if(cmd_buffer[2] == '0')
			{
				// buzzer off
				BUZZER_OFF();
			}
			else
			{
				// buzzer on
				BUZZER_ON();
			}
		}
		
		/* Receive the full buffer command. */
		UART_ReceivePayload(cmd_buffer, 10);		
	}
	
	//address = 0;
}

void AC_ON()
{
	PORTD |= 1<<5;
}

void AC_OFF()
{
	PORTD &= ~(1<<5);
}

void BUZZER_ON()
{
	PORTD |= 1<<6;
}

void BUZZER_OFF()
{
	PORTD &= ~(1<<6);
}

int GetAC()
{
	return PIND & 1<<5;
}

int GetBuzzer()
{
	if(buzzer_override)
		return 0;
	return PIND & 1<<6;
}